using System;
using System.Runtime.CompilerServices;

namespace DotJEM.Diagnostics.Streams;

public static class InfoStreamExtensions
{
    public static void WriteError<TSource>(this IInfoStream<TSource> self, Exception exception, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0) 
        => self.WriteEvent(new InfoStreamExceptionEvent(typeof(TSource), InfoLevel.ERROR, exception.Message, callerMemberName, callerFilePath, callerLineNumber, exception));

    public static void WriteError<TSource>(this IInfoStream<TSource> self, string message, Exception exception, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0) 
        => self.WriteEvent(new InfoStreamExceptionEvent(typeof(TSource),InfoLevel.ERROR, message, callerMemberName, callerFilePath, callerLineNumber, exception));

    public static void WriteWarning<TSource>(this IInfoStream<TSource> self, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0) 
        => self.WriteEvent(new InfoStreamEvent(typeof(TSource),InfoLevel.WARNING, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteInfo<TSource>(this IInfoStream<TSource> self, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0) 
        => self.WriteEvent(new InfoStreamEvent(typeof(TSource),InfoLevel.INFO, message, callerMemberName, callerFilePath, callerLineNumber));

    public static void WriteDebug<TSource>(this IInfoStream<TSource> self, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0) 
        => self.WriteEvent(new InfoStreamEvent(typeof(TSource),InfoLevel.DEBUG, message, callerMemberName, callerFilePath, callerLineNumber));
}