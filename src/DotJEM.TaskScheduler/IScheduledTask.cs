using System;
using System.Runtime.CompilerServices;
using DotJEM.Diagnostics.Streams;

namespace DotJEM.TaskScheduler;

public interface IScheduledTask : IDisposable
{
    event EventHandler<EventArgs> TaskDisposed;

    Guid Id { get; }
    string Name { get; }
    public IInfoStream InfoStream { get; }

    IScheduledTask Start();

    /// <summary>
    /// Signals that the task should run immediately regardless of its trigger.
    /// </summary>
    IScheduledTask Signal();

    /// <summary>
    /// Signals that the task should run immediately after the provided delay regardless of its trigger.
    /// </summary>
    IScheduledTask Signal(TimeSpan delay);

    TaskAwaiter<int> GetAwaiter();
}