using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        var detail = await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        Assert.NotNull(created.DueAtUtc);
        Assert.NotNull(detail.Triage);
        Assert.Equal(TicketPriority.High, detail.Triage.Priority);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        var detail = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
        Assert.NotNull(detail.Triage);
        Assert.Equal(TicketPriority.Medium, detail.Triage.Priority);
    }

    [Fact]
    public async Task HandleAsync_ForcesCriticalPriority_WhenCategoryConfigured()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category("Outage", requiresSpecialist: false, forcesCriticalPriority: true));

        var detail = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Critical, context.AddedTicket.Priority);
        Assert.NotNull(detail.Triage);
        Assert.Equal(TicketPriority.Critical, detail.Triage.Priority);
    }

    [Fact]
    public async Task HandleAsync_AssignsAvailableAgent_WhenEligibleAgentExists()
    {
        var context = new TicketCommandTestContext();
        var agent = new AgentDto(7, "Sarah Jenkins", "sarah@example.com", true, 5, 1, []);
        context.SetAvailableAgents(agent);

        var detail = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(7, context.AddedTicket.AssignedAgentId);
        Assert.NotNull(detail.Triage);
        Assert.Equal(7, detail.Triage.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupportDesk.Domain.Aggregates.Customers.Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }
}
