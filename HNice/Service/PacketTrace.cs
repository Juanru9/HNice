using HNice.Model;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace HNice.Service;

/// <summary>
/// Optional full packet trace: every packet, both directions, one line each, appended to a file.
/// Enabled by starting HNice with <c>--packet-trace &lt;file&gt;</c> (or the HNICE_PACKET_TRACE environment variable).
/// Lines are written on a background task, so tracing never slows the relay.
///   HH:mm:ss.fff → SETSTUFFDATA        AJ@I999000002@DTRUE
/// Packets are byte-exact in the [1] [2] [9] [10] [13] notation, so any line can be pasted into the composer.
/// </summary>
public sealed class PacketTrace : IDisposable
{
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _writer;

    public string Path { get; }

    private PacketTrace(string path)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        _writer = Task.Run(WriteLoopAsync);
    }

    /// <summary>A trace when requested on the command line or through the environment, otherwise null.</summary>
    public static PacketTrace? FromStartup(ILogger logger)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.FindIndex(args, a => a.Equals("--packet-trace", StringComparison.OrdinalIgnoreCase));
        var path = index >= 0 && index + 1 < args.Length ? args[index + 1] : Environment.GetEnvironmentVariable("HNICE_PACKET_TRACE");
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            var trace = new PacketTrace(System.IO.Path.GetFullPath(path));
            logger.LogInformation("Packet trace: writing every packet to {Path}", trace.Path);
            return trace;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Packet trace disabled: cannot write to {Path}", path);
            return null;
        }
    }

    // Client packets carrying secrets or identifiers: the trace is a plain file on disk, so these are never written.
    private static readonly HashSet<string> Redacted = new(StringComparer.Ordinal) { "TRY_LOGIN", "UNIQUEID" };

    public void Write(PacketLogEntry entry)
    {
        var packet = !entry.IsInbound && Redacted.Contains(entry.HeaderName)
            ? $"{entry.Header}[redacted, {entry.Raw.Length - 2} chars]"
            : entry.Escaped;
        _lines.Writer.TryWrite($"{entry.TimeText} {(entry.IsInbound ? "←" : "→")} {entry.HeaderName,-20} {packet}");
    }

    public void Note(string text) => _lines.Writer.TryWrite($"{DateTime.Now:HH:mm:ss.fff} # {text}");

    private async Task WriteLoopAsync()
    {
        await using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteLineAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} # HNice packet trace started");

        while (await _lines.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (_lines.Reader.TryRead(out var line))
            {
                await writer.WriteLineAsync(line).ConfigureAwait(false);
            }
            // Flush per batch so the file can be read live.
            await writer.FlushAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _lines.Writer.TryComplete();
        _writer.Wait(TimeSpan.FromSeconds(2));
    }
}
