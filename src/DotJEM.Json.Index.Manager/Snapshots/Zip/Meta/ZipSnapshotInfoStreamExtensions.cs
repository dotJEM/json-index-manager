using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.Json.Index2.Snapshots.Zip;
using DotJEM.ObservableExtensions.InfoStreams;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip.Meta;

public static class ZipSnapshotInfoStreamExtensions
{
    public static void WriteSnapshotOpenEvent<TSource>(this IInfoStream<TSource> self, MetaZipFileSnapshot snapshot, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipSnapshotEvent(typeof(TSource), InfoLevel.INFO, snapshot, FileEventType.OPEN, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteSnapshotCloseEvent<TSource>(this IInfoStream<TSource> self, MetaZipFileSnapshot snapshot, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipSnapshotEvent(typeof(TSource), InfoLevel.INFO, snapshot, FileEventType.CLOSE, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteFileOpenEvent<TSource>(this IInfoStream<TSource> self, ISnapshotFile file, string message, FileProgress progress,[CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipFileEvent(typeof(TSource), InfoLevel.INFO, file, FileEventType.OPEN, message,progress, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteFileCloseEvent<TSource>(this IInfoStream<TSource> self, ISnapshotFile file, string message, FileProgress progress,[CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipFileEvent(typeof(TSource), InfoLevel.INFO, file, FileEventType.CLOSE, message,progress, callerMemberName, callerFilePath, callerLineNumber));
    public static void WriteFileProgressEvent<TSource>(this IInfoStream<TSource> self, ISnapshotFile file, string message, FileProgress progress, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipFileEvent(typeof(TSource), InfoLevel.INFO, file, FileEventType.PROGRESS, message, progress, callerMemberName, callerFilePath, callerLineNumber));
}


public record struct SnapshotRestoreState(SnapshotFileRestoreState[] Files) : ITrackerState
{
    public DateTime StartTime { get; set; } = DateTime.Now;

    public override string ToString()
    {
        return Files
            .OrderBy(f => f.State).ThenBy(f => f.Name)
            .Aggregate(new StringBuilder()
                    .AppendLine($"Restoring {Files.Length} files from snapshot."),
                (sb, state) => sb.AppendLine(state.ToString()))
            .ToString();
    }

}


public record struct SnapshotFileRestoreState(string Name, string State, DateTime StartTime, DateTime StopTime, FileProgress Progress)
{
    public TimeSpan Duration => StartTime - StopTime;

    public override string ToString()
    {
        return $" -> {Name}: {State} [{FormatBytes()}] {(Progress.Copied / Progress.Size) * 100}%";
    }
    
    private const long KiloByte = 1024;
    private const long MegaByte = KiloByte * KiloByte;
    private const long GigaByte = MegaByte * KiloByte;
    private const long TeraByte = GigaByte * KiloByte;

    private string FormatBytes()
    {
        const int offset = 100;
        switch (Progress.Size)
        {
            case (< KiloByte * offset):
                return $"{Progress.Copied}/{Progress.Size} Bytes";
            case (>= KiloByte * offset) and (< MegaByte * offset):
                return $"{Progress.Copied / KiloByte}/{Progress.Size / KiloByte} KiloBytes";
            case (>= MegaByte * offset) and (< GigaByte * offset):
                return $"{Progress.Copied / MegaByte}/{Progress.Size / MegaByte} MegaBytes";
            case (>= GigaByte * offset) and (< TeraByte * offset):
                return $"{Progress.Copied / GigaByte}/{Progress.Size / GigaByte} GigaBytes";
            case (>= TeraByte * offset):
                return $"{Progress.Copied / TeraByte}/{Progress.Size / TeraByte} TeraBytes";
        }
        //...
    }
}
