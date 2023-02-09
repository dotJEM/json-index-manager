using System.Runtime.CompilerServices;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Storage.Snapshot;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public static class ZipInfoStreamExtensions
{
    public static void WriteSnapshotOpenEvent<TSource>(this IInfoStream<TSource> self, LuceneZipSnapshot snapshot, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipSnapshotEvent(typeof(TSource), InfoLevel.INFO, snapshot, FileEventType.OPEN, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteSnapshotCloseEvent<TSource>(this IInfoStream<TSource> self, LuceneZipSnapshot snapshot, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipSnapshotEvent(typeof(TSource), InfoLevel.INFO, snapshot, FileEventType.CLOSE, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteFileOpenEvent<TSource>(this IInfoStream<TSource> self, LuceneZipFile file, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipFileEvent(typeof(TSource), InfoLevel.INFO, file, FileEventType.OPEN, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteFileCloseEvent<TSource>(this IInfoStream<TSource> self, LuceneZipFile file, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => self.WriteEvent(new ZipFileEvent(typeof(TSource), InfoLevel.INFO, file, FileEventType.CLOSE, message, callerMemberName, callerFilePath, callerLineNumber));
}