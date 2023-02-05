using System;

namespace DotJEM.Json.Index.Manager.Diagnostics;

public interface IInfoStream : IObservable<IInfoStreamEvent>
{
    void Forward(IInfoStream infoStream);
    void WriteEvent(IInfoStreamEvent evt);
}

public interface IInfoStream<TOwner> : IInfoStream
{
}