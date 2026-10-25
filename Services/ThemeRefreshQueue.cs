namespace Lumina.Services;

// One native refresh at a time, with at most one follow-up for newer requests.
internal sealed class ThemeRefreshQueue(Action refresh) : IDisposable
{
    private readonly object _gate = new();
    private bool _pending, _running, _disposed;
    private Task _completion = Task.CompletedTask;
    internal Task Completion { get { lock (_gate) return _completion; } }

    internal void Request()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending = true;
            if (_running) return;
            _running = true;
            _completion = Task.Run(Drain);
        }
    }

    private void Drain()
    {
        while (true)
        {
            lock (_gate)
            {
                if (_disposed || !_pending) { _running = false; return; }
                _pending = false;
            }
            try { refresh(); }
            catch (Exception ex) { App.Log($"[ThemeService] Shell refresh failed: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        lock (_gate) { _disposed = true; _pending = false; }
    }
}
