using System;
using System.Collections.Generic;

namespace DotJEM.ObservableExt;

public static class ObservableSubscriptionExtensions
{
    public static ObservableSubscription Attach<TObserver>(this IDictionary<Guid, TObserver> dictionary, TObserver observer)
    {
        Guid id = Guid.NewGuid();
        dictionary.Add(id, observer);
        return new ObservableSubscription(() => dictionary.Remove(id));
    }
}