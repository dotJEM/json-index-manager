using System;

namespace DotJEM.ObservableExt;

public interface IForwarderObservable<T> : IObservable<T>, IDisposable
{
    void Publish(T value);
    void Raise(Exception ex);
    IDisposable Forward(IForwarderObservable<T> piped);
}