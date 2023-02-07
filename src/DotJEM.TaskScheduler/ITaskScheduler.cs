namespace DotJEM.TaskScheduler;

public interface ITaskScheduler
{
    IScheduledTask Schedule(IScheduledTask task);
    void Stop();
}