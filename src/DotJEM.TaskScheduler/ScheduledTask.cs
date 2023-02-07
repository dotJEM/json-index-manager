using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.TaskScheduler.Abstractions;
using DotJEM.TaskScheduler.Triggers;

namespace DotJEM.TaskScheduler;

public class ScheduledTask : Disposable, IScheduledTask
{
    public event EventHandler<EventArgs> TaskDisposed;

    private readonly object padlock = new();
    private readonly Action<bool> callback;
    private readonly AutoResetEvent handle = new(false);
    private readonly ActivitySource activitySource;
    private readonly IThreadPool pool;
    private readonly ITrigger trigger;
    private readonly IInfoStream<ScheduledTask> infoStream = new DefaultInfoStream<ScheduledTask>();
    private readonly TaskCompletionSource<int> completeCompletionSource;

    private RegisteredWaitHandle executing;
    private bool started = false;

    public IInfoStream InfoStream => infoStream;

    public Guid Id { get; }
    public string Name { get; }

    public ScheduledTask(string name, Action<bool> callback, ITrigger trigger)
        : this(Guid.NewGuid(), name, callback, new ThreadPoolProxy(), trigger) { }
    
    public ScheduledTask(Guid id, string name, Action<bool> callback, ITrigger trigger)
        : this(id, name, callback, new ThreadPoolProxy(), trigger) { }

    public ScheduledTask(string name, Action<bool> callback, IThreadPool pool, ITrigger trigger)
        : this(Guid.NewGuid(), name, callback, pool, trigger) { }

    public ScheduledTask(Guid id, string name, Action<bool> callback, IThreadPool pool, ITrigger trigger)
    {
        Id = id;
        this.Name = name;
        this.callback = callback;
        this.pool = pool;
        this.trigger = trigger;
        this.activitySource = ActivitySources.Create<ScheduledTask>();
        completeCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    
    public IScheduledTask Start()
    {
        lock (padlock)
        {
            if (started) return this;
            started = true;

            if (trigger.TryGetNext(true, out TimeSpan value))
                RegisterWait(value);
            return this;
        }
    }

    public TaskAwaiter<int> GetAwaiter() => completeCompletionSource.Task.GetAwaiter();

    /// <summary>
    /// Registers the next call for the scheduled task onto the threadpool.
    /// </summary>
    /// <remarks>
    /// If the task has been disposed this method dows nothing.
    /// </remarks>
    /// <param name="timeout">Time untill next execution</param>
    /// <returns>self</returns>
    protected virtual IScheduledTask RegisterWait(TimeSpan timeout)
    {
        if (Disposed)
            return this;

        executing = pool.RegisterWaitForSingleObject(handle, (state, timedout) => ExecuteCallback(timedout), null, timeout, true);
        return this;
    }

    protected virtual bool ExecuteCallback(bool timedout)
    {
        if (Disposed)
            return false;

        using Activity activity = activitySource.StartActivity(Name);
        try
        {
            callback(!timedout);
            activity?.SetTag(nameof(timedout), timedout);
            return true;
        }
        catch (Exception ex)
        {
            infoStream.WriteError($"Failed to execute task '{Name}'.", ex);
            activity?.SetTag("exception", ex);
            return false;
        }
        finally
        {
            if (trigger.TryGetNext(false, out TimeSpan value))
                RegisterWait(value);

        }
    }

    /// <summary>
    /// Marks the task for shutdown and signals any waiting tasks.
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        if(Disposed)
            return;

        base.Dispose(disposing);
        executing?.Unregister(null);
        Signal();

        infoStream.WriteTaskCompleted($"Task '{Name}' completed.");
        infoStream.Dispose();

        TaskDisposed?.Invoke(this, EventArgs.Empty);
        completeCompletionSource.SetResult(0);
    }

    public virtual IScheduledTask Signal()
    {
        handle.Set();
        return this;
    }

    public IScheduledTask Signal(TimeSpan delay)
    {
        pool.RegisterWaitForSingleObject(new AutoResetEvent(false), (_, _) => Signal(), null, delay, true);
        return this;
    }
}