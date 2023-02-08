using System.Runtime.CompilerServices;
using DotJEM.Diagnostics.Streams;

namespace DotJEM.Json.Index.Manager;

public static class IndexManagerInfoStreamExtensions
{
    public static void WriteStorageObserverEvent<TSource>(this IInfoStream<TSource> self, StorageObserverEventType eventType, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        self.WriteEvent(new StorageObserverInfoStreamEvent<TSource>("INFO", eventType, message, callerMemberName, callerFilePath, callerLineNumber));
    }
}
public enum StorageObserverEventType
{
    Initializing, Initialized, Updating, Updated, Stopped
}

public class StorageObserverInfoStreamEvent<TSource> : InfoStreamEvent<TSource>
{
    public StorageObserverEventType EventType { get; }

    public StorageObserverInfoStreamEvent(string level, StorageObserverEventType eventType, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        EventType = eventType;
    }

    public override string ToString()
    {
        return $"[{Level}] {EventType}:{Message} ({Source} {CallerMemberName} - {CallerFilePath}:{CallerLineNumber})";
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



