using Serilog.Core;
using Serilog.Events;

namespace UserService.IntegrationTests.Support;

public sealed class CapturedLogSink : ILogEventSink
{
    private readonly object gate = new();
    private readonly List<LogEvent> events = [];

    public void Emit(LogEvent logEvent)
    {
        lock (gate)
        {
            events.Add(logEvent);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            events.Clear();
        }
    }

    public IReadOnlyList<LogEvent> Snapshot()
    {
        lock (gate)
        {
            return events.ToArray();
        }
    }
}
