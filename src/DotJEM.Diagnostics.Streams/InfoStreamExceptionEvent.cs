using System;

namespace DotJEM.Diagnostics.Streams;

public class InfoStreamExceptionEvent<TSource> : InfoStreamEvent<TSource>
{
    public Exception Exception { get; }

    public InfoStreamExceptionEvent(string level, string message, string callerMemberName, string callerFilePath, int callerLineNumber, Exception exception)
        : base(level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        Exception = exception;
    }
}