namespace AWS.Core.Models;

public enum LogLevel { Info, Warn, Error }

public class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Source { get; init; }

    public string LevelTag => Level switch
    {
        LogLevel.Warn  => "WARN",
        LogLevel.Error => "ERR ",
        _              => "INFO",
    };
}
