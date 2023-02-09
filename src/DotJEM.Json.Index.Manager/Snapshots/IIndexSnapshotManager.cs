using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface IIndexSnapshotManager
{
    IInfoStream InfoStream { get; }

    bool TakeSnapshot();
    bool RestoreSnapshot();
}

public class IndexSnapshotManager : IIndexSnapshotManager
{
    private IInfoStream<IndexSnapshotManager> infoStream = new InfoStream<IndexSnapshotManager>();
    public IInfoStream InfoStream { get; }
    public bool TakeSnapshot()
    {
        throw new NotImplementedException();
    }

    public bool RestoreSnapshot()
    {
        throw new NotImplementedException();
    }
}