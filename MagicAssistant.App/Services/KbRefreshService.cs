
namespace MagicAssistant.App.Services;

/// <summary>
/// Canonical Blazor pattern for cross-thread UI notifications.
/// Microsoft Learn: "In the event a component must be updated based on an external event,
/// use InvokeAsync to dispatch code execution to Blazor's synchronization context."
/// 
/// Registered as Scoped — one instance per circuit. Background tasks (Task.Run) call Notify(),
/// and the subscribing component wraps its handler in InvokeAsync for proper marshaling.
/// </summary>
public sealed class KbRefreshService : IDisposable
{
    public async Task Notify()
    {
        if (_notify != null)
        {
            await _notify();
        }
    }

    private Func<Task>? _notify;

    /// <summary>Subscribe a component to refresh notifications. Call from OnInitialized.</summary>
    public void Subscribe(Func<Task> handler) => _notify += handler;

    /// <summary>Unsubscribe. Call from Dispose.</summary>
    public void Unsubscribe(Func<Task> handler) => _notify -= handler;

    public void Dispose() => _notify = null;
}
