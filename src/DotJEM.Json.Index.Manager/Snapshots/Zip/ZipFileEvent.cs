using System;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.ObservableExtensions.InfoStreams;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipFileEvent : InfoStreamEvent
{
    private readonly ILuceneFile file;

    public FileEventType EventType { get; }
    //public LuceneZipFile File { get; }

    public string FileName => file.Name;

    public ZipFileEvent(Type source, InfoLevel level, ILuceneFile file, FileEventType eventType, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(source, level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        this.file = file;
        EventType = eventType;
    }
}