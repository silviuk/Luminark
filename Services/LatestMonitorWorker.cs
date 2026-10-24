namespace Lumina.Services;

internal enum MonitorCommandKind { Brightness, Volume }

// One in-flight command and at most one pending value per command kind.
internal sealed class LatestMonitorWorker
{
    private readonly object _gate = new();
    private readonly Dictionary<MonitorCommandKind, Action> _pending = new();
    private readonly CancellationTokenSource _stop = new();
    private Task _running = Task.CompletedTask;
    private bool _stopped;
    private bool _isRunning;
    private MonitorCommandKind? _lastKind;

    internal int PendingCount { get { lock (_gate) return _pending.Count; } }
    internal Task Completion { get { lock (_gate) return _running; } }

    internal void Schedule(MonitorCommandKind kind, Action apply)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _pending[kind] = apply;
            if (!_isRunning)
            {
                _isRunning = true;
                _running = Task.Run(RunAsync);
            }
        }
    }

    private async Task RunAsync()
    {
        var token = _stop.Token;
        try
        {
            while (true)
            {
                await Task.Delay(40, token).ConfigureAwait(false);
                Action command;
                lock (_gate)
                {
                    if (_stopped || _pending.Count == 0)
                    {
                        _isRunning = false;
                        return;
                    }
                    var next = _pending.Count > 1
                        ? _pending.First(item => item.Key != _lastKind)
                        : _pending.First();
                    _lastKind = next.Key;
                    command = next.Value;
                    _pending.Remove(next.Key);
                }
                try { command(); }
                catch (Exception ex) { App.Log($"[MonitorWorker] {ex.Message}"); }
            }
        }
        catch (OperationCanceledException) { }
    }
    internal Task StopAsync()
    {
        lock (_gate)
        {
            if (!_stopped)
            {
                _stopped = true;
                _pending.Clear();
                _stop.Cancel();
                _ = _running.ContinueWith(_ => _stop.Dispose(), TaskScheduler.Default);
            }
            return _running;
        }
    }
}
