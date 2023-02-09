using System;
using DotJEM.Diagnostics.Streams;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipSnapshotEvent : InfoStreamEvent
{
    
    public FileEventType EventType { get; }
    public LuceneZipSnapshot Snapshot { get; }

    public ZipSnapshotEvent(Type source, InfoLevel level, LuceneZipSnapshot snapshot, FileEventType eventType, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(source, level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        Snapshot = snapshot;
        EventType = eventType;
    }
}