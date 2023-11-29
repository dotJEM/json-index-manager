using System;
using System.IO;
using System.Linq;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.Json.Index2.Snapshots.Zip;
using DotJEM.ObservableExtensions.InfoStreams;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipSnapshotStrategy : ISnapshotStrategy
{
    private readonly string path;
    private readonly int maxSnapshots;
    private readonly IInfoStream<ZipSnapshotStrategy> infoStream = new InfoStream<ZipSnapshotStrategy>();

    public IInfoStream InfoStream => infoStream;

    public ZipSnapshotStrategy(string path, int maxSnapshots = 2)
    {
        this.path = path;
        this.maxSnapshots = maxSnapshots;
    }


    public ISnapshotStorage OpenStorage()
    {
        return new ZipSnapshotStorage(path);
    }

    public void CleanOldSnapshots()
    {
        foreach (string file in GetSnapshots().Skip(maxSnapshots))
        {
            try
            {
                File.Delete(file);
                infoStream.WriteInfo($"Deleted snapshot: {file}");
            }
            catch (Exception ex)
            {
                infoStream.WriteError($"Failed to delete snapshot: {file}", ex);
            }
        }
    }

    private string[] GetSnapshots()
    {
        if (!Directory.Exists(path))
            return Array.Empty<string>();

        return Directory.GetFiles(path, "*.zip")
            .OrderByDescending(file => file)
            .ToArray();
    }

}


