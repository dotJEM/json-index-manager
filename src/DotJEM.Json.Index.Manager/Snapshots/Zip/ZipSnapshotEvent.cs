using System;
using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Index2.Snapshots.Zip;
using DotJEM.ObservableExtensions.InfoStreams;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipSnapshotEvent : InfoStreamEvent
{
    private readonly ZipFileSnapshot snapshot;

    public FileEventType EventType { get; }
    
    //public IEnumerable<string> SnapshotFiles => snapshot.Files.Select(file => file.Name);

    public ZipSnapshotEvent(Type source, InfoLevel level, ZipFileSnapshot snapshot, FileEventType eventType, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(source, level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        this.snapshot = snapshot;
        EventType = eventType;
    }
}

