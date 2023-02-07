using System;

namespace DotJEM.TaskScheduler.Triggers;

public class PeriodicTrigger : ITrigger
{
    private readonly TimeSpan timeSpan;

    public PeriodicTrigger(TimeSpan timeSpan)
    {
        this.timeSpan = timeSpan;
    }

    public bool TryGetNext(bool firstExecution, out TimeSpan timeSpan)
    {
        timeSpan = this.timeSpan;
        return true;
    }
}