using AWS.Core.Models;
using System.Collections.ObjectModel;

namespace AWS.Core.Interfaces;

public interface ILogService
{
    ObservableCollection<LogEntry> Entries { get; }
    void Info(string message, string? source = null);
    void Warn(string message, string? source = null);
    void Error(string message, string? source = null);
    void Clear();
}
