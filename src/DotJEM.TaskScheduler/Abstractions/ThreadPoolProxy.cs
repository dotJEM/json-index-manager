using System;
using System.Threading;

namespace DotJEM.TaskScheduler.Abstractions;

public class ThreadPoolProxy : IThreadPool
{
    public RegisteredWaitHandle RegisterWaitForSingleObject(WaitHandle handle, WaitOrTimerCallback callback, object state, TimeSpan timeout, bool executeOnlyOnce)
    {
        return ThreadPool.RegisterWaitForSingleObject(handle, callback, state, timeout, executeOnlyOnce);
    }
}