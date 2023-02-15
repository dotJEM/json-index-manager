﻿using System;
using System.Collections.Generic;

namespace DotJEM.ObservableExt;

public interface IForwarderObservable<T> : IObservable<T>, IDisposable
{
    void Publish(T value);
    void Raise(Exception ex);
    IDisposable Forward(IForwarderObservable<T> piped);
}

public class ForwarderObservable<T> : IForwarderObservable<T>
{
    private readonly Dictionary<Guid, IObserver<T>> subscribers = new();

    public IDisposable Subscribe(IObserver<T> observer) 
        => subscribers.Attach(observer);

    public void Publish(T value)
    {
        foreach (IObserver<T> observer in subscribers.Values)
        {
            try
            {
                observer.OnNext(value);
            }
            catch (Exception e)
            {
                observer.OnError(e);
            }
        }
    }

    public void Dispose()
    {
        foreach (IObserver<T> observer in subscribers.Values)
            observer.OnCompleted();
    }

    public void Raise(Exception ex)
    {
        foreach (IObserver<T> observer in subscribers.Values)
            observer.OnError(ex);
    }

    public IDisposable Forward(IForwarderObservable<T> piped)
    {
        return Subscribe(new Forwarder(piped));
    }

    public class Forwarder : IObserver<T>
    {
        private readonly IForwarderObservable<T> observable;

        public Forwarder(IForwarderObservable<T> observable)
        {
            this.observable = observable;
        }

        public void OnNext(T value) => observable.Publish(value);
        public void OnError(Exception error) => observable.Raise(error);
        public void OnCompleted() => observable.Dispose();
    }
}