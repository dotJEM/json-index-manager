namespace DotJEM.TaskScheduler;

public interface IWebBackgroundTaskScheduler
{
    IScheduledTask Schedule(IScheduledTask task);
    void Stop();
}