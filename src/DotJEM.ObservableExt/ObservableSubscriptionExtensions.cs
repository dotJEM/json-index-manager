using System;
using System.Collections.Generic;

namespace DotJEM.ObservableExt;

public static class ObservableSubscriptionExtensions
{
    public static ObservableSubscription Attach<TObserver>(this IDictionary<Guid, TObserver> dictionary, TObserver observer)
    {
        Guid id = Guid.NewGuid();
        dictionary.Add(id, observer);
        //Note: Making the dictionary a weak reference here means the source observable can be fully collected by the GC
        //      including it's subscriber tracking even though there are still live subscribers. These subscribers won't receive
        //      anything from this subscription in this event anyways.
        WeakReference<IDictionary<Guid, TObserver>> reference = new(dictionary);
        return new ObservableSubscription(() => {
            if(reference.TryGetTarget(out IDictionary<Guid, TObserver> x))
                x.Remove(id);
        });
    }
}