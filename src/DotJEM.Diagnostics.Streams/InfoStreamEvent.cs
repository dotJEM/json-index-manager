using System;

namespace DotJEM.Diagnostics.Streams;

public class InfoStreamEvent<TSource> : IInfoStreamEvent
{
    public Type Source { get; } = typeof(TSource);
    public string Level { get; }
    public string Message { get; }
    public string CallerMemberName { get; }
    public string CallerFilePath { get; }
    public int CallerLineNumber { get; }

    public InfoStreamEvent(string level, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
    {
        Level = level;
        Message = message;
        CallerMemberName = callerMemberName;
        CallerFilePath = callerFilePath;
        CallerLineNumber = callerLineNumber;
    }
}