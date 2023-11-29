using System;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.ObservableExtensions.InfoStreams;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip.Meta;

public class ZipFileEvent : InfoStreamEvent
{
    private readonly ISnapshotFile file;

    public FileEventType EventType { get; }
    //public LuceneZipFile File { get; }

    public string FileName => file.Name;

    public ZipFileEvent(Type source, InfoLevel level, ISnapshotFile file, FileEventType eventType, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(source, level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        this.file = file;
        EventType = eventType;
    }
}