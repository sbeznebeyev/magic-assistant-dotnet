using MagicAssistant.Core;

namespace MagicAssistant.Tests.Infrastructure;

[TestFixture]
public class SyncSetStateRegistryTests
{
    private SyncSetStateRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new SyncSetStateRegistry();
    }

    // ─── Register / Get ────────────────────────────────────

    [Test]
    public void Register_Then_Get_ReturnsSameInstance()
    {
        var state = new SyncSetState("m19");
        _registry.Register(state);

        Assert.That(_registry.Get("m19"), Is.SameAs(state),
            "BUG in Register/Get: Get should return the exact same instance that was registered. " +
            "If it returns a different object, the registry may be cloning states or storing by value.");
    }

    [Test]
    public void Get_NonExistent_ReturnsNull()
    {
        var result = _registry.Get("NONEXIST");

        Assert.That(result, Is.Null,
            "BUG in Get: Requesting a non-registered set abbreviation should return null, not throw.");
    }

    [Test]
    public void Register_OverwritesExisting()
    {
        var state1 = new SyncSetState("m19");
        _registry.Register(state1);

        var state2 = new SyncSetState("m19");
        _registry.Register(state2);

        Assert.That(_registry.Get("m19"), Is.SameAs(state2),
            "BUG in Register: Re-registering the same abbreviation should replace the old state. " +
            "Got the original instance instead of the new one.");
    }

    // ─── Remove ────────────────────────────────────────────

    [Test]
    public void Remove_RemovesState_And_KeepsOthers()
    {
        _registry.Register(new SyncSetState("m19"));
        _registry.Register(new SyncSetState("m20"));
        _registry.Remove("m19");

        Assert.That(_registry.Get("m19"), Is.Null);
        Assert.That(_registry.Get("m20"), Is.Not.Null);
    }

    [Test]
    public void Register_After_Remove_ReplacesClean()
    {
        var state1 = new SyncSetState("m19");
        _registry.Register(state1);
        _registry.Remove("m19");

        var state2 = new SyncSetState("m19");
        _registry.Register(state2);

        Assert.That(_registry.Get("m19"), Is.SameAs(state2),
            "BUG in Register after Remove: Re-registering a previously removed abbreviation should work cleanly. " +
            "Got a stale or null reference instead of the new state.");
        Assert.That(_registry.Get("m19"), Is.Not.SameAs(state1),
            "BUG: After Remove+Register, Get returned the OLD state instance that was already removed.");
    }

    // ─── GetAllActive ──────────────────────────────────────

    [Test]
    public void GetAllActive_ReturnsCorrectCount()
    {
        _registry.Register(new SyncSetState("m19"));
        _registry.Register(new SyncSetState("m20"));

        var all = _registry.GetAllActive();
        Assert.That(all, Has.Count.EqualTo(2),
            "BUG in GetAllActive: Should return exactly the number of registered states.");
    }

    [Test]
    public void GetAllActive_ReturnsSnapshot_NotLiveReference()
    {
        _registry.Register(new SyncSetState("m19"));
        var snapshot = _registry.GetAllActive();

        _registry.Register(new SyncSetState("m20"));
        _registry.Remove("m19");

        // Snapshot must be unaffected by subsequent mutations
        Assert.That(snapshot, Has.Count.EqualTo(1),
            "BUG in GetAllActive: Returned collection is a live view instead of a snapshot. " +
            "Mutations after GetAllActive should not affect the previously returned collection.");
    }

    // ─── Thread safety ─────────────────────────────────────

    [Test]
    public void Concurrent_Registers_AllItemsPersist()
    {
        const int count = 50;

        // Parallel.For blocks until all iterations complete, so no extra sync needed
        Parallel.For(0, count, i =>
        {
            _registry.Register(new SyncSetState($"set-{i}"));
        });

        var all = _registry.GetAllActive();
        Assert.That(all, Has.Count.EqualTo(count),
            "BUG in concurrent Register: Some registered states were lost during concurrent access. " +
            "The lock in Register may not be protecting the dictionary correctly, " +
            "or the dictionary implementation has a race condition.");
    }

    [Test]
    public void Concurrent_MixedOperations_NoDataLoss()
    {
        // Pre-register items
        for (int i = 0; i < 20; i++)
            _registry.Register(new SyncSetState($"set-{i}"));

        var exceptions = new List<Exception>();
        var lockObj = new object();

        Parallel.For(0, 100, _ =>
        {
            try
            {
                // Random mix of reads and removes
                var idx = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 20);
                _registry.Get($"set-{idx}");
                _registry.GetAllActive();
                if (idx < 10)
                    _registry.Remove($"set-{idx}");
            }
            catch (Exception ex)
            {
                lock (lockObj)
                    exceptions.Add(ex);
            }
        });

        Assert.That(exceptions, Is.Empty,
            "BUG in concurrent access: Thread-safe operations threw exceptions under load. " +
            "The lock may not cover all critical sections or the dictionary is being modified while enumerated.");
    }
}
