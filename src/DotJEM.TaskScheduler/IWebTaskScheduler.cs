namespace DotJEM.TaskScheduler;

public interface IWebTaskScheduler
{
    IScheduledTask Schedule(IScheduledTask task);
    void Stop();
}