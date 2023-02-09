using System;
using System.Collections.Generic;
using DotJEM.ObservableExt;

namespace DotJEM.Diagnostics.Streams;

public class InfoStream<TOwner> : ForwarderObservable<IInfoStreamEvent>, IInfoStream<TOwner>
{
    public void WriteEvent(IInfoStreamEvent evt) => Publish(evt);
}