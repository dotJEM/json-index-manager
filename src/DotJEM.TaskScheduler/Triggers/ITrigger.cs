using System;

namespace DotJEM.TaskScheduler.Triggers;

public interface ITrigger
{
    bool TryGetNext(bool firstExecution, out TimeSpan timeSpan);
}