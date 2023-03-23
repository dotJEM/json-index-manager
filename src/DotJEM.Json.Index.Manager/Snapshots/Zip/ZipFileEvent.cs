using System;
using DotJEM.ObservableExtensions.InfoStreams;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipFileEvent : InfoStreamEvent
{
    public FileEventType EventType { get; }
    public LuceneZipFile File { get; }

    public ZipFileEvent(Type source, InfoLevel level, LuceneZipFile file, FileEventType eventType, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(source, level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        File = file;
        EventType = eventType;
    }
}