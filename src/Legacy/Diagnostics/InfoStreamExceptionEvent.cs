using System;

namespace DotJEM.Json.Index.Manager.Diagnostics;

public class InfoStreamExceptionEvent<TSource> : InfoStreamEvent<TSource>
{
    public Exception Exception { get; }

    public InfoStreamExceptionEvent(string level, string message, string callerMemberName, string callerFilePath, int callerLineNumber, Exception exception)
        : base(level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        Exception = exception;
    }
}