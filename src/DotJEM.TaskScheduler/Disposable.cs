using System;

namespace DotJEM.TaskScheduler;

public abstract class Disposable : IDisposable
{
    protected volatile bool Disposed;

    protected virtual void Dispose(bool disposing)
    {
        Disposed = true;
    }

    ~Disposable()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}