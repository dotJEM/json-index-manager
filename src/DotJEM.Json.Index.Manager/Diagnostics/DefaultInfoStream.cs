using System;
using System.Collections.Generic;
using DotJEM.Json.Index.Manager.Observable;

namespace DotJEM.Json.Index.Manager.Diagnostics;

public class DefaultInfoStream<TOwner> : IInfoStream<TOwner>
{
    private readonly Dictionary<Guid, IObserver<IInfoStreamEvent>> subscribers = new();

    public void Forward(IInfoStream infoStream)
    {
        Subscribe(new ForwardingSubscriber(infoStream));
    }

    public void WriteEvent(IInfoStreamEvent evt)
    {
        foreach (IObserver<IInfoStreamEvent> observer in subscribers.Values)
        {
            try
            {
                observer.OnNext(evt);
            }
            catch (Exception e)
            {
                observer.OnError(e);
            }
        }
    }

    public IDisposable Subscribe(IObserver<IInfoStreamEvent> observer)
    {
        return subscribers.Attach(observer);
    }

    private class ForwardingSubscriber : IObserver<IInfoStreamEvent>
    {
        private readonly IInfoStream infoStream;

        public ForwardingSubscriber(IInfoStream infoStream)
        {
            this.infoStream = infoStream;
        }

        public void OnNext(IInfoStreamEvent value)
        {
            infoStream.WriteEvent(value);
        }

        public void OnError(Exception error)
        {
            //TODO?
        }

        public void OnCompleted()
        {
            //Info Streams does not complete.
        }
    }

}