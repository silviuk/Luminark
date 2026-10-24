using System.IO;
using System.Threading.Channels;

namespace Lumina.Services;

internal sealed class AsyncFileLogger : IDisposable
{
    private readonly Channel<string> _messages = Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropWrite
    });
    private readonly Task _writer;
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly bool _debug = Environment.GetEnvironmentVariable("LUMINARK_LOG_LEVEL") == "Debug";

    internal AsyncFileLogger(string path, long maxBytes = 5 * 1024 * 1024)
    {
        _path = path;
        _maxBytes = maxBytes;
        _writer = Task.Run(WriteAsync);
    }

    internal void Write(string message, bool debug = false)
    {
        if (debug && !_debug) return;
        if (message.Length > 8192) message = message[..8192];
        _messages.Writer.TryWrite($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }

    private async Task WriteAsync()
    {
        while (await _messages.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                if (File.Exists(_path) && new FileInfo(_path).Length >= _maxBytes)
                    File.Move(_path, _path + ".old", true);
                using var writer = new StreamWriter(_path, append: true);
                int batch = 0;
                while (batch++ < 128 && _messages.Reader.TryRead(out var message))
                    await writer.WriteLineAsync(message).ConfigureAwait(false);
            }
            catch (IOException) { DiscardBatch(); }
            catch (UnauthorizedAccessException) { DiscardBatch(); }
        }
    }

    private void DiscardBatch()
    {
        for (int i = 0; i < 128 && _messages.Reader.TryRead(out _); i++) { }
    }

    internal Task Completion => _writer;

    public void Dispose()
    {
        _messages.Writer.TryComplete();
        // A stalled disk must not prevent application shutdown.
        try { _writer.Wait(TimeSpan.FromSeconds(1)); } catch { }
    }
}
