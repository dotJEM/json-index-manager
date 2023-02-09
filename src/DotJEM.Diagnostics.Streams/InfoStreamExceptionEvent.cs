using System;

namespace DotJEM.Diagnostics.Streams;

public class InfoStreamExceptionEvent : InfoStreamEvent
{
    public Exception Exception { get; }

    public InfoStreamExceptionEvent(Type source, InfoLevel level, string message, string callerMemberName, string callerFilePath, int callerLineNumber, Exception exception)
        : base(source, level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
        Exception = exception;
    }
}