using AWS.Core.Interfaces;
using AWS.Core.Models;
using System.Collections.ObjectModel;
using System.IO;

namespace AWS.Services;

public class LogService : ILogService
{
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LxAws", "logs");

    private const int MaxEntries = 500;

    // 在 UI 线程（WPF 应用主线程）创建时捕获同步上下文
    private readonly SynchronizationContext? _uiContext;

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public LogService()
    {
        _uiContext = SynchronizationContext.Current;
    }

    public void Info(string message, string? source = null) =>
        Append(LogLevel.Info, message, source);

    public void Warn(string message, string? source = null) =>
        Append(LogLevel.Warn, message, source);

    public void Error(string message, string? source = null) =>
        Append(LogLevel.Error, message, source);

    public void Clear() => RunOnUi(Entries.Clear);

    private void Append(LogLevel level, string message, string? source)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source,
        };

        RunOnUi(() =>
        {
            if (Entries.Count >= MaxEntries)
                Entries.RemoveAt(0);
            Entries.Add(entry);
        });

        WriteToFileAsync(entry);
    }

    private void RunOnUi(Action action)
    {
        if (_uiContext == null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    private static void WriteToFileAsync(LogEntry entry)
    {
        Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                var file = Path.Combine(LogDir, $"weighing_{entry.Timestamp:yyyyMMdd}.log");
                var src = entry.Source is not null ? $"[{entry.Source}] " : string.Empty;
                var line = $"{entry.Timestamp:HH:mm:ss.fff} [{entry.LevelTag}] {src}{entry.Message}{Environment.NewLine}";
                File.AppendAllText(file, line, System.Text.Encoding.UTF8);
            }
            catch { /* 日志写入失败不影响主程序 */ }
        });
    }
}
