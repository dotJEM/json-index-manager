using DotJEM.ObservableExt;
using System;

namespace DotJEM.Diagnostics.Streams;

public interface IInfoStream : IForwarderObservable<IInfoStreamEvent>
{
    void WriteEvent(IInfoStreamEvent evt);
}

public interface IInfoStream<TOwner> : IInfoStream
{
}