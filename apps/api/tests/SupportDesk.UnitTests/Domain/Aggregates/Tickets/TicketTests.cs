using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(3);

    [Fact]
    public void Raise_StartsNewUnassignedAndWithoutADueDate()
    {
        var ticket = Ticket.Raise(
            "TCK-0041", "  Cannot log in  ", "  Happens for every user.  ", 1, 3,
            TicketPriority.High, FixedClock.DefaultNow);

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal("Cannot log in", ticket.Title);
        Assert.Equal("Happens for every user.", ticket.Description);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenResolving_StampsResolutionAndUpdateTimes()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.InProgress).Build();

        ticket.ChangeStatus(TicketStatus.Resolved, Later);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(Later, ticket.ResolvedAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
        Assert.False(ticket.IsOpen);
    }

    [Fact]
    public void ChangeStatus_WhenReopening_ClearsTheResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.InProgress, Later);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAnUnresolvedTicket_RecordsAResolutionTime()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Open).Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(Later, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAResolvedTicket_KeepsTheOriginalResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(FixedClock.DefaultNow, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenTicketIsAlreadyClosed_Throws()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0026")
            .WithStatus(TicketStatus.Closed)
            .Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.ChangeStatus(TicketStatus.Open, Later));

        Assert.Contains("TCK-0026", exception.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void AssignTo_WithoutAnAgent_TakesTheTicketBack()
    {
        var ticket = new TicketBuilder().AssignedTo(2).Build();

        ticket.AssignTo(null, Later);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void CanEscalate_ReturnsTrue_WhenTicketIsEligible()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).WithStatus(TicketStatus.Open).Build();

        var canEscalate = ticket.CanEscalate(out var rejectionReason);

        Assert.True(canEscalate);
        Assert.Null(rejectionReason);
    }

    [Fact]
    public void CanEscalate_ReturnsFalse_WhenTicketIsCritical()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();

        var canEscalate = ticket.CanEscalate(out var rejectionReason);

        Assert.False(canEscalate);
        Assert.Contains("Critical", rejectionReason);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void CanEscalate_ReturnsFalse_WhenTicketIsResolvedOrClosed(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.High).WithStatus(status).Build();

        var canEscalate = ticket.CanEscalate(out var rejectionReason);

        Assert.False(canEscalate);
        Assert.Contains(status.ToString(), rejectionReason);
    }

    [Fact]
    public void Escalate_UpdatesPriorityDueAtAndAssignee()
    {
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .AssignedTo(1)
            .Build();

        var newDue = FixedClock.DefaultNow.AddHours(4);
        ticket.Escalate(TicketPriority.High, newDue, 2, Later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(newDue, ticket.DueAtUtc);
        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalate_WhenCritical_ThrowsBusinessRuleViolationException()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate(TicketPriority.Critical, Later, 1, Later));
    }

    [Fact]
    public void Escalate_WhenResolvedOrClosed_ThrowsBusinessRuleViolationException()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Resolved).Build();

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate(TicketPriority.High, Later, 1, Later));
    }
}
