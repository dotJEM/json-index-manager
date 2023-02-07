using DotJEM.Diagnostics.Streams;

namespace DotJEM.TaskScheduler;

public class TaskCompletedInfoStreamEvent<TSource> : InfoStreamEvent<TSource>
{
    public TaskCompletedInfoStreamEvent(string level, string message, string callerMemberName, string callerFilePath, int callerLineNumber) 
        : base(level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
    }
}