using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;

namespace HNice.Service;

/// <summary>
/// Developer command channel: when HNice is started with <c>--command-file &lt;file&gt;</c>, every line appended to that
/// file is run as a command. Lets a console session drive HNice (send packets, fetch the catalogue) without the UI.
/// Off unless the flag is given.
///   server &lt;packet&gt;        send to the server (header + body, [2] notation)
///   client &lt;packet&gt;        send to your client
///   catalog fetch [pages]  request catalogue pages (all when none given)
/// </summary>
public sealed class CommandChannel : IDisposable
{
    private readonly string _path;
    private readonly Func<string, Task> _run;
    private readonly ILogger _logger;
    private readonly Timer _timer;
    private long _offset;
    private int _busy;

    private CommandChannel(string path, Func<string, Task> run, ILogger logger)
    {
        _path = path;
        _run = run;
        _logger = logger;
        // Only commands written after start-up are run.
        _offset = File.Exists(path) ? new FileInfo(path).Length : 0;
        _timer = new Timer(_ => Poll(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    }

    public static CommandChannel? FromStartup(ILogger logger, Func<string, Task> run)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.FindIndex(args, a => a.Equals("--command-file", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length) return null;

        var path = Path.GetFullPath(args[index + 1]);
        logger.LogInformation("Command channel: reading commands from {Path}", path);
        return new CommandChannel(path, run, logger);
    }

    private async void Poll()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            if (!File.Exists(_path)) return;
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length < _offset) _offset = 0; // file was replaced
            if (stream.Length == _offset) return;

            stream.Seek(_offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            var lastNewLine = text.LastIndexOf('\n');
            if (lastNewLine < 0) return; // wait for a complete line

            _offset += Encoding.UTF8.GetByteCount(text[..(lastNewLine + 1)]);
            foreach (var line in text[..lastNewLine].Split('\n'))
            {
                var command = line.Trim('\r', ' ');
                if (command.Length == 0 || command.StartsWith('#')) continue;
                try
                {
                    await _run(command);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Command failed: {Command}", command);
                }
            }
        }
        catch (IOException)
        {
            // File busy: try again on the next tick.
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    public void Dispose() => _timer.Dispose();
}
