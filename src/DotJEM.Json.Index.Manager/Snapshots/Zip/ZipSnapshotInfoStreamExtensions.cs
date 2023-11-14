using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.Json.Index2.Snapshots.Zip;
using DotJEM.ObservableExtensions.InfoStreams;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public static class ZipSnapshotInfoStreamExtensions
{
    public static void WriteSnapshotOpenEvent<TSource>(this IInfoStream<TSource> self, ZipFileSnapshot snapshot, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipSnapshotEvent(typeof(TSource), InfoLevel.INFO, snapshot, FileEventType.OPEN, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteSnapshotCloseEvent<TSource>(this IInfoStream<TSource> self, ZipFileSnapshot snapshot, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipSnapshotEvent(typeof(TSource), InfoLevel.INFO, snapshot, FileEventType.CLOSE, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteFileOpenEvent<TSource>(this IInfoStream<TSource> self, ILuceneFile file, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipFileEvent(typeof(TSource), InfoLevel.INFO, file, FileEventType.OPEN, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteFileCloseEvent<TSource>(this IInfoStream<TSource> self, ILuceneFile file, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipFileEvent(typeof(TSource), InfoLevel.INFO, file, FileEventType.CLOSE, message, callerMemberName, callerFilePath, callerLineNumber));
}


public record struct SnapshotRestoreState(SnapshotFileRestoreState[] Files) : ITrackerState
{
    public DateTime StartTime { get; set; } = DateTime.Now;
    
    public override string ToString()
    {
        return Files.Aggregate(new StringBuilder()
                    .AppendLine($"Restoring {Files.Length} files from snapshot."),
                (sb, state) => sb.AppendLine(state.ToString()))
            .ToString();
    }

}


public record struct SnapshotFileRestoreState(string Name, string State, DateTime StartTime, DateTime StopTime)
{
    public TimeSpan Duration => StartTime - StopTime;

    public override string ToString()
    {
        return $" -> {Name} : {State}";
    }
}
