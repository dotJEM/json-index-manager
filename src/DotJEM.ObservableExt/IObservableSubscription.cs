using System;

namespace DotJEM.ObservableExt;

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