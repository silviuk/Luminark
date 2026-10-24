namespace Lumina.Services;

// Input hooks accumulate steps in constant space and schedule at most one delivery.
internal sealed class ScrollStepDispatcher(Action<int> deliver) : IDisposable
{
    private int _steps;
    private int _scheduled;
    private int _disposed;

    internal void Add(int steps)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        Interlocked.Add(ref _steps, steps);
        if (Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0)
            ThreadPool.QueueUserWorkItem(_ => Drain());
    }

    private void Drain()
    {
        while (Volatile.Read(ref _disposed) == 0)
        {
            int steps = Interlocked.Exchange(ref _steps, 0);
            try { if (steps != 0) deliver(steps); }
            catch (Exception ex) { App.Log($"[TrayScroll] {ex.Message}"); }
            Interlocked.Exchange(ref _scheduled, 0);
            if (Volatile.Read(ref _steps) == 0 || Interlocked.CompareExchange(ref _scheduled, 1, 0) != 0) return;
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
        Interlocked.Exchange(ref _steps, 0);
    }
}
