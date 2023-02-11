using System;

namespace DotJEM.Diagnostics.Streams;

public class InfoStreamEvent : IInfoStreamEvent
{
    private readonly Lazy<string> message;

    public Type Source { get; }
    public InfoLevel Level { get; }
    public string Message => message.Value;
    public string CallerMemberName { get; }
    public string CallerFilePath { get; }
    public int CallerLineNumber { get; }

    public InfoStreamEvent(Type source, InfoLevel level, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : this(source, level, () => message, callerMemberName, callerFilePath, callerLineNumber){}

    public InfoStreamEvent(Type source, InfoLevel level, Func<string> messageFactory, string callerMemberName, string callerFilePath, int callerLineNumber)
    {
        Source = source;
        Level = level;
        CallerMemberName = callerMemberName;
        CallerFilePath = callerFilePath;
        CallerLineNumber = callerLineNumber;

        message = new Lazy<string>(messageFactory);
    }

    public override string ToString()
        => $"[{Level}] {Message} ({Source} {CallerMemberName} - {CallerFilePath}:{CallerLineNumber})";
}