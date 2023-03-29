using System;
using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Index.Storage.Snapshot;
using DotJEM.ObservableExtensions.InfoStreams;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipSnapshotEvent : InfoStreamEvent
{
    private readonly LuceneZipSnapshot snapshot;

    public FileEventType EventType { get; }


    public string SegmentsFileName => snapshot.SegmentsFile.Name;
    public string SegmentsGenFileName => snapshot.SegmentsGenFile.Name;
    public IEnumerable<string> SnapshotFiles => snapshot.Files.Select(file => file.Name);

    public ZipSnapshotEvent(Type source, InfoLevel level, LuceneZipSnapshot snapshot, FileEventType eventType, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(source, level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        this.snapshot = snapshot;
        EventType = eventType;
    }
}