using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static EscalateTicketRequest ValidRequest(
        string reason = "Customer escalated due to business impact.",
        string actor = "support.lead@example.com") =>
        new(reason, actor);

    [Fact]
    public async Task HandleAsync_RaisesPriorityByOneLevel_RecalculatesDueAt_AndRecordsAuditRow()
    {
        var context = new TicketCommandTestContext();
        var originalDue = FixedClock.DefaultNow.AddHours(24);
        var ticket = context.ExistingTicket(new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .WithStatus(TicketStatus.Open)
            .WithDueAt(originalDue)
            .AssignedTo(1)
            .Build());

        var agent1 = new AgentDto(1, "Agent One", "agent1@example.com", true, 10, 2, []);
        context.SetAvailableAgents(agent1);

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, ValidRequest(), CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.NotNull(ticket.DueAtUtc);
        Assert.NotEqual(originalDue, ticket.DueAtUtc);

        // Verification of audit record
        var escalation = context.AddedEscalation;
        Assert.NotNull(escalation);
        Assert.Equal(ticket.Id, escalation.TicketId);
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(1, escalation.FromAgentId);
        Assert.Equal(1, escalation.ToAgentId);
        Assert.Equal(originalDue, escalation.FromDueAtUtc);
        Assert.Equal(ticket.DueAtUtc, escalation.ToDueAtUtc);
        Assert.Equal("Customer escalated due to business impact.", escalation.Reason);
        Assert.Equal("support.lead@example.com", escalation.EscalatedBy);
        Assert.Equal(FixedClock.DefaultNow, escalation.EscalatedAtUtc);

        Assert.Equal(escalation.Id, result.Escalation.Id);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsCritical_ThrowsBusinessRuleViolationException()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder()
            .WithPriority(TicketPriority.Critical)
            .WithStatus(TicketStatus.Open)
            .Build());

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, ValidRequest(), CancellationToken.None));

        Assert.Contains("Critical", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsResolved_ThrowsBusinessRuleViolationException()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .WithStatus(TicketStatus.Resolved)
            .Build());

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, ValidRequest(), CancellationToken.None));

        Assert.Contains("Resolved", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsClosed_ThrowsBusinessRuleViolationException()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder()
            .WithPriority(TicketPriority.High)
            .WithStatus(TicketStatus.Closed)
            .Build());

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, ValidRequest(), CancellationToken.None));

        Assert.Contains("Closed", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")] // < 5 chars
    public async Task HandleAsync_WhenReasonIsTooShort_ThrowsValidationException(string shortReason)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .WithStatus(TicketStatus.Open)
            .Build());

        var request = new EscalateTicketRequest(shortReason, "actor@example.com");

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, request, CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey(nameof(EscalateTicketRequest.Reason)));
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenAssignedAgentAtCapacity_ReassignsToEligibleAgent()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .WithStatus(TicketStatus.Open)
            .AssignedTo(1)
            .Build());

        // Agent 1 is at max capacity (5/5), Agent 2 has capacity (2/5)
        var agent1 = new AgentDto(1, "Agent One", "agent1@example.com", true, 5, 5, []);
        var agent2 = new AgentDto(2, "Agent Two", "agent2@example.com", true, 5, 2, []);
        context.SetAvailableAgents(agent1, agent2);

        await context.EscalateTicket.HandleAsync(ticket.Id, ValidRequest(), CancellationToken.None);

        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(2, context.AddedEscalation?.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenAssignedAgentStillEligible_RetainsAgent()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .WithStatus(TicketStatus.Open)
            .AssignedTo(1)
            .Build());

        // Agent 1 still has capacity (3/5), Agent 2 has lower count (1/5)
        // Per BR-7: keep current agent if still eligible!
        var agent1 = new AgentDto(1, "Agent One", "agent1@example.com", true, 5, 3, []);
        var agent2 = new AgentDto(2, "Agent Two", "agent2@example.com", true, 5, 1, []);
        context.SetAvailableAgents(agent1, agent2);

        await context.EscalateTicket.HandleAsync(ticket.Id, ValidRequest(), CancellationToken.None);

        Assert.Equal(1, ticket.AssignedAgentId);
        Assert.Equal(1, context.AddedEscalation?.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketNotFound_ThrowsNotFoundException()
    {
        var context = new TicketCommandTestContext();
        context.Tickets
            .Setup(t => t.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, ValidRequest(), CancellationToken.None));

        Assert.Contains("Ticket", ex.Message);
        context.VerifySaved(Times.Never());
    }
}
