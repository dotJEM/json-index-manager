using System;
using System.Threading;

namespace DotJEM.TaskScheduler.Abstractions;

public interface IThreadPool
{
    RegisteredWaitHandle RegisterWaitForSingleObject(WaitHandle handle, WaitOrTimerCallback callback, object state, TimeSpan timeout, bool executeOnlyOnce);
}