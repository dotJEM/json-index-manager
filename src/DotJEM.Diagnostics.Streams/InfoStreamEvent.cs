using System;

namespace DotJEM.Diagnostics.Streams;

public class InfoStreamEvent : IInfoStreamEvent
{
    public Type Source { get; }
    public InfoLevel Level { get; }
    public string Message { get; }
    public string CallerMemberName { get; }
    public string CallerFilePath { get; }
    public int CallerLineNumber { get; }
    public InfoStreamEvent(Type source, InfoLevel level, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
    {
        Source = source;
        Level = level;
        Message = message;
        CallerMemberName = callerMemberName;
        CallerFilePath = callerFilePath;
        CallerLineNumber = callerLineNumber;
    }

    public override string ToString()
        => $"[{Level}] {Message} ({Source} {CallerMemberName} - {CallerFilePath}:{CallerLineNumber})";
}