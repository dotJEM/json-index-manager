using System;
using System.Runtime.CompilerServices;
using DotJEM.Diagnostics.Streams;

namespace DotJEM.Json.Index.Manager;

public static class IndexManagerInfoStreamExtensions
{
    public static void WriteStorageObserverEvent<TSource>(this IInfoStream<TSource> self, StorageObserverEventType eventType, string area, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        self.WriteEvent(new StorageObserverInfoStreamEvent(typeof(TSource), InfoLevel.INFO, eventType, area, message, callerMemberName, callerFilePath, callerLineNumber));
    }
}
public enum StorageObserverEventType
{
    Initializing, Initialized, Updating, Updated, Stopped
}

public interface IStorageInfoStreamEvent : IInfoStreamEvent
{
    StorageObserverEventType EventType { get; }
}

public class StorageObserverInfoStreamEvent : InfoStreamEvent, IStorageInfoStreamEvent
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



//public class StorageObserverInitializingInfoStreamEvent<TSource> : StorageObserverInfoStreamEvent<TSource>
//{
//    public StorageObserverInitializingInfoStreamEvent(string level, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
//        : base(level, message, callerMemberName, callerFilePath, callerLineNumber)
//    {
//    }
//}

//public class StorageObserverInitializedInfoStreamEvent<TSource> : StorageObserverInfoStreamEvent<TSource>
//{
//    public StorageObserverInitializedInfoStreamEvent(string level, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
//        : base(level, message, callerMemberName, callerFilePath, callerLineNumber)
//    {
//    }
//}



