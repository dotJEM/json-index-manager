using System;
using System.Runtime.CompilerServices;

namespace DotJEM.Diagnostics.Streams;

public static class InfoStreamExtensions
{
    public static void WriteError<TSource>(this IInfoStream<TSource> self, Exception exception, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        self.WriteEvent(new InfoStreamExceptionEvent<TSource>("ERROR", exception.Message, callerMemberName, callerFilePath, callerLineNumber, exception));
    }

    public static void WriteError<TSource>(this IInfoStream<TSource> self, string message, Exception exception, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        self.WriteEvent(new InfoStreamExceptionEvent<TSource>("ERROR", message, callerMemberName, callerFilePath, callerLineNumber, exception));
    }

    public static void WriteInfo<TSource>(this IInfoStream<TSource> self, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        self.WriteEvent(new InfoStreamEvent<TSource>("INFO", message, callerMemberName, callerFilePath, callerLineNumber));
    }

    public static void WriteDebug<TSource>(this IInfoStream<TSource> self, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        self.WriteEvent(new InfoStreamEvent<TSource>("DEBUG", message, callerMemberName, callerFilePath, callerLineNumber));
    }
}