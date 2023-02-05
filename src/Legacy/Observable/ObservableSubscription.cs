using System;
using System.Collections.Generic;

namespace DotJEM.Json.Index.Manager.Observable
{
    public interface IObservableSubscription : IDisposable
    {
    }

    public class ObservableSubscription : IObservableSubscription
    {
        private readonly Action onDetach;

        public ObservableSubscription(Action onDetach)
        {
            this.onDetach = onDetach;
        }

        public void Dispose() => onDetach();
    }

    public abstract class AbstractObserveable<T> : IObservable<T>, IDisposable
    {
        private readonly Dictionary<Guid, IObserver<T>> subscribers = new();

        public IDisposable Subscribe(IObserver<T> observer) => subscribers.Attach(observer);

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

        private void Raise(Exception ex)
        {
            foreach (IObserver<T> observer in subscribers.Values)
                observer.OnError(ex);
        }

        public void Forward(AbstractObserveable<T> piped)
        {
            Subscribe(new Forwarder(piped));
        }

        public class Forwarder : IObserver<T>
        {
            private readonly AbstractObserveable<T>  observable;

            public Forwarder(AbstractObserveable<T> observable)
            {
                this.observable = observable;
            }

            public void OnNext(T value) => observable.Publish(value);
            public void OnError(Exception error) => observable.Raise(error);
            public void OnCompleted() => observable.Dispose();
        }
    }
    

    public static class ObservableSubscriptionExtensions {
        public static ObservableSubscription Attach<TObserver>(this IDictionary<Guid, TObserver> dictionary, TObserver observer)
        {
            Guid id = Guid.NewGuid();
            dictionary.Add(id, observer);
            return new ObservableSubscription(() => dictionary.Remove(id));
        }
    }
}
