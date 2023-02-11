using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DotJEM.Diagnostics.Streams;

namespace DotJEM.Json.Index.Manager;

public static class IndexManagerInfoStreamExtensions
{
    public static void WriteStorageObserverEvent<TSource>(this IInfoStream<TSource> self, StorageObserverEventType eventType, string area, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        self.WriteEvent(new StorageObserverInfoStreamEvent(typeof(TSource), InfoLevel.INFO, eventType, area, message, callerMemberName, callerFilePath, callerLineNumber));
    }

    public static void WriteStorageIngestStateEvent<TSource>(this IInfoStream<TSource> self, StorageIngestState state, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        self.WriteEvent(new StorageIngestStateInfoStreamEvent(typeof(TSource), InfoLevel.INFO, state, callerMemberName, callerFilePath, callerLineNumber));
    }
}

public record struct StorageIngestState(AreaIngestState[] Areas)
{
    public DateTime StartTime => Areas.Min(x => x.StartTime);
    public TimeSpan Duration => Areas.Max(x => x.Duration);
    public long IngestedCount => Areas.Sum(x => x.IngestedCount);
    public GenerationInfo Generation => Areas.Select(x => x.Generation).Aggregate((left, right) => left + right);

    public override string ToString()
    {
        TimeSpan duration = Duration;
        GenerationInfo generation = Generation;
        long count = IngestedCount;
        return Areas.Aggregate(new StringBuilder()
                    .AppendLine($"[{duration:d\\.hh\\:mm\\:ss}] {generation.Current:N0} of {generation.Latest:N0} changes processed, {count:N0} objects indexed. ({count / duration.TotalSeconds:F} / sec)"),
                        (sb, state) => sb.AppendLine(state.ToString()))
                    .ToString();
    }
}

public record struct AreaIngestState(string Area, DateTime StartTime, TimeSpan Duration, long IngestedCount, GenerationInfo Generation, StorageObserverEventType State)
{
    public override string ToString()
    {
        return $" -> [{Duration:d\\.hh\\:mm\\:ss}] {Area} {Generation.Current:N0} of {Generation.Latest:N0} changes processed, {IngestedCount:N0} objects indexed. ({IngestedCount / Duration.TotalSeconds:F} / sec) - {State}";
    }
}

public class StorageIngestStateInfoStreamEvent : InfoStreamEvent
{
    public StorageIngestState State { get; }

    public StorageIngestStateInfoStreamEvent(Type source, InfoLevel level, StorageIngestState state, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(source, level, state.ToString, callerMemberName, callerFilePath, callerLineNumber)
    {
        State = state;
    }
}


public enum StorageObserverEventType
{
    Starting, Initializing, Initialized, Updating, Updated, Stopped
}

public class StorageObserverInfoStreamEvent : InfoStreamEvent
{
    public string Area { get; }
    public StorageObserverEventType EventType { get; }

    public StorageObserverInfoStreamEvent(Type source, InfoLevel level, StorageObserverEventType eventType, string area, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(source, level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        EventType = eventType;
        Area = area;
    }

    public override string ToString()
    {
        return $"[{Level}] {Area}:{EventType}:{Message} ({Source} {CallerMemberName} - {CallerFilePath}:{CallerLineNumber})";
    }
}

