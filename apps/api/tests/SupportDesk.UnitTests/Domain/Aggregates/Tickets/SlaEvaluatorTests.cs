using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndComfortablyBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(10), null, Now.AddHours(-10), Now));

    [Fact]
    public void Evaluate_OpenWithExactly25PercentWindowRemaining_IsAtRisk()
    {
        // 4 hour total window: Created 3 hours ago, Due in 1 hour (1/4 = 25% remaining)
        var createdAt = Now.AddHours(-3);
        var dueAt = Now.AddHours(1);

        var status = SlaEvaluator.Evaluate(dueAt, null, createdAt, Now);

        Assert.Equal(SlaStatus.AtRisk, status);
    }

    [Fact]
    public void Evaluate_OpenWithLessThan25PercentWindowRemaining_IsAtRisk()
    {
        // 4 hour total window: Created 3.5 hours ago, Due in 30 minutes (0.5/4 = 12.5% remaining)
        var createdAt = Now.AddHours(-3.5);
        var dueAt = Now.AddMinutes(30);

        var status = SlaEvaluator.Evaluate(dueAt, null, createdAt, Now);

        Assert.Equal(SlaStatus.AtRisk, status);
    }

    [Fact]
    public void Evaluate_OpenWithMoreThan25PercentWindowRemaining_IsWithinSla()
    {
        // 4 hour total window: Created 2 hours ago, Due in 2 hours (2/4 = 50% remaining)
        var createdAt = Now.AddHours(-2);
        var dueAt = Now.AddHours(2);

        var status = SlaEvaluator.Evaluate(dueAt, null, createdAt, Now);

        Assert.Equal(SlaStatus.WithinSla, status);
    }
}
