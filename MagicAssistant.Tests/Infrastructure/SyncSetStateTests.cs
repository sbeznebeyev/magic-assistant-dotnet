using MagicAssistant.Core;

namespace MagicAssistant.Tests.Infrastructure;

[TestFixture]
public class SyncSetStateTests
{
    // ─── Constructor / Defaults ────────────────────────────

    [Test]
    public void Constructor_SetsAbbreviationAndStartedAt()
    {
        var before = DateTimeOffset.UtcNow;
        var state = new SyncSetState("m19");
        Assert.That(state.SetCode, Is.EqualTo("m19"));
        Assert.That(state.StartedAt, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public void Defaults_NumericFields_AreZero()
    {
        var state = new SyncSetState("m19");
        Assert.Multiple(() =>
        {
            Assert.That(state.TotalCards, Is.EqualTo(0));
            Assert.That(state.ReadyCardsToSave, Is.EqualTo(0));
            Assert.That(state.ImagesDownloaded, Is.EqualTo(0));
            Assert.That(state.ImagesFailed, Is.EqualTo(0));
            Assert.That(state.Phase, Is.EqualTo(0));
        });
    }

    [Test]
    public void Defaults_BooleanAndStringFields_AreDefault()
    {
        var state = new SyncSetState("m19");
        Assert.Multiple(() =>
        {
            Assert.That(state.Completed, Is.False);
            Assert.That(state.Failed, Is.False);
            Assert.That(state.ErrorMessage, Is.Null);
            Assert.That(state.PhaseName, Is.EqualTo(string.Empty));
            Assert.That(state.Message, Is.EqualTo(string.Empty));
        });
    }

    // ─── ReadyCardsToSave capping ──────────────────────────

    [Test]
    public void ReadyCardsToSave_CappedAtTotal_WhenTotalIsPositive()
    {
        var state = new SyncSetState("m19");
        state.TotalCards = 100;
        state.ReadyCardsToSave = 200;

        Assert.That(state.ReadyCardsToSave, Is.EqualTo(100),
            "BUG in ReadyCardsToSave setter: Value exceeding TotalCards should be capped to TotalCards.");
    }

    [Test]
    public void ReadyCardsToSave_NotCapped_WhenTotalIsZero()
    {
        // BUG: The setter uses 'if (value > TotalCards && TotalCards > 0)' —
        // when TotalCards == 0, the cap is skipped entirely. This is intentional
        // (can't cap against zero), but the test documents this behavior explicitly.
        var state = new SyncSetState("m19");
        // TotalCards stays 0 (default)
        state.ReadyCardsToSave = 50;

        Assert.That(state.ReadyCardsToSave, Is.EqualTo(50),
            "When TotalCards is 0, ReadyCardsToSave is NOT capped. " +
            "This is because the setter condition 'TotalCards > 0' prevents capping against zero. " +
            "If this changes unexpectedly, the capping logic in the setter may have been modified.");
    }

    // ─── ProgressPercent ───────────────────────────────────

    [Test]
    public void ProgressPercent_ReturnsZero_WhenTotalCardsIsZero()
    {
        var state = new SyncSetState("m19");
        Assert.That(state.ProgressPercent, Is.EqualTo(0),
            "ProgressPercent should return 0 when TotalCards is 0 (division-by-zero guard).");
    }

    [Test]
    public void ProgressPercent_CalculatesCorrectly()
    {
        // Phase-weighted formula: download owns 0-35, mapping 35-70 (see SyncSetState.ProgressPercent).
        var state = new SyncSetState("m19");
        state.TotalCards = 100;
        state.Phase = 1;
        state.ReadyCardsToSave = 50;

        Assert.That(state.ProgressPercent, Is.EqualTo(17.5),
            "Download phase: half of 100 cards fills half of the 0-35 band.");

        state.Phase = 2;
        Assert.That(state.ProgressPercent, Is.EqualTo(52.5),
            "Mapping phase: the same 50/100 now fills the 35-70 band.");
    }

    [Test]
    public void ProgressPercent_CappedAt100()
    {
        // Dual protection: the setter caps ReadyCardsToSave at TotalCards, and every phase band
        // caps with Math.Min — no input may exceed 100 or leak into the next band.
        var state = new SyncSetState("m19");
        state.TotalCards = 100;
        state.Phase = 1;
        state.ReadyCardsToSave = 150;  // setter caps at TotalCards

        Assert.That(state.ProgressPercent, Is.EqualTo(35),
            "Download band saturates at 35, never leaks into mapping.");

        state.Phase = 3;
        state.ImagesDownloaded = 180;
        state.ImagesFailed = 40;  // 220 image events for 100 cards
        Assert.That(state.ProgressPercent, Is.EqualTo(95),
            "Image band saturates at 95, never reaches the done value.");

        state.Phase = 5;
        Assert.That(state.ProgressPercent, Is.EqualTo(100));
    }

    // ─── Elapsed ───────────────────────────────────────────

    [Test]
    public void Elapsed_ReturnsZero_WhenNotFinished()
    {
        var state = new SyncSetState("m19");

        Assert.That(state.Elapsed, Is.EqualTo(TimeSpan.Zero),
            "Elapsed should be TimeSpan.Zero (default) when neither Completed nor Failed is set.");
    }

    [Test]
    public void Elapsed_ReturnsPositive_WhenCompleted()
    {
        var state = new SyncSetState("m19");
        // Small delay to ensure measurable elapsed time
        Thread.Sleep(50);
        state.Completed = true;

        Assert.That(state.Elapsed, Is.GreaterThan(TimeSpan.Zero),
            "Elapsed should be positive after Completed is set.");
    }

    [Test]
    public void Elapsed_ReturnsPositive_WhenFailed()
    {
        var state = new SyncSetState("m19");
        // Small delay to ensure measurable elapsed time
        Thread.Sleep(50);
        state.Failed = true;

        Assert.That(state.Elapsed, Is.GreaterThan(TimeSpan.Zero),
            "Elapsed should be positive after Failed is set.");
    }

    [Test]
    public void Elapsed_ReturnsZero_AfterResettingCompleted()
    {
        var state = new SyncSetState("m19");
        state.Completed = true;
        Assert.That(state.Elapsed, Is.GreaterThan(TimeSpan.Zero));

        // Reset — elapsed should go back to zero
        state.Completed = false;

        Assert.That(state.Elapsed, Is.EqualTo(TimeSpan.Zero),
            "Elapsed should return to TimeSpan.Zero when Completed is reset to false. " +
            "The property checks 'Completed || Failed' inline, so both must be false.");
    }

    // ─── Thread safety ─────────────────────────────────────

    [Test]
    public void ReadyCardsToSave_DirectSet_IsThreadSafe()
    {
        // Direct assignment (not increment) IS thread-safe because the setter holds a lock.
        // Multiple threads setting absolute values won't corrupt internal state.
        var state = new SyncSetState("m19");
        state.TotalCards = 100;

        Parallel.For(0, 10, _ =>
        {
            for (int i = 0; i < 100; i++)
                state.ReadyCardsToSave = 50; // direct set, not increment
        });

        Assert.That(state.ReadyCardsToSave, Is.EqualTo(50),
            "Direct assignment to ReadyCardsToSave should be thread-safe. " +
            "All threads set the same value (50), so the final result must be 50. " +
            "If it differs, the setter's lock is not protecting the backing field.");
    }

    [Test]
    public void ReadyCardsToSave_Capping_HoldsUnderConcurrency()
    {
        // Even with concurrent writes, the cap should never be violated.
        var state = new SyncSetState("m19");
        state.TotalCards = 100;

        Parallel.For(0, 50, _ =>
        {
            for (int i = 0; i < 100; i++)
                state.ReadyCardsToSave = 999; // well above cap
        });

        Assert.That(state.ReadyCardsToSave, Is.LessThanOrEqualTo(100),
            $"BUG: ReadyCardsToSave ({state.ReadyCardsToSave}) exceeds TotalCards (100). " +
            $"The capping logic in the setter is not holding under concurrent access. " +
            $"The lock may not be protecting the comparison-and-cap correctly.");
    }
}
