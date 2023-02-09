using System;
using DotJEM.Diagnostics.Streams;

namespace DotJEM.TaskScheduler;

public class TaskCompletedInfoStreamEvent : InfoStreamEvent
{
    public TaskCompletedInfoStreamEvent(Type source, InfoLevel level, string message, string callerMemberName, string callerFilePath, int callerLineNumber) 
        : base(source, level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
    }
}