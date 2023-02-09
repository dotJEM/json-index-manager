// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index;
using DotJEM.Json.Index.Analyzation;
using DotJEM.Json.Index.Configuration;
using DotJEM.Json.Index.Manager;
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Configuration;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Version = Lucene.Net.Util.Version;

//TraceSource trace; 

var storage = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=ssn3db;Integrated Security=True");
storage.Configure.MapField(JsonField.Id, "id");
storage.Configure.MapField(JsonField.ContentType, "contentType");
storage.Configure.MapField(JsonField.Version, "$version");
storage.Configure.MapField(JsonField.Created, "$created");
storage.Configure.MapField(JsonField.Updated, "$updated");
storage.Configure.MapField(JsonField.SchemaVersion, "$schemaVersion");

var index = new LuceneStorageIndex(new LuceneFileIndexStorage(".\\app_data\\index", new StandardAnalyzer(Version.LUCENE_30, new SortedSet<string>())));
index.Configuration.SetTypeResolver("contentType");
index.Configuration.SetRawField("$raw");
index.Configuration.SetScoreField("$score");
index.Configuration.SetIdentity("id");
index.Configuration.SetSerializer(new ZipJsonDocumentSerializer());

IStorageManager storageManager = new StorageManager(storage, new DotJEM.TaskScheduler.TaskScheduler());
IIndexManager manager = new IndexManager(storageManager, new IndexSnapshotManager(), new WriteContextFactory(index));

Task run = Task.WhenAll(
    storageManager.Observable.ForEachAsync(Reporter.Capture),
    storageManager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
    manager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
    Task.Run(storageManager.RunAsync)
);


while (true)
{
    switch (Console.ReadLine())
    {
        case "EXIT":
            goto EXIT;

        default:
            Reporter.Report();
            break;
    }
}

EXIT:
await run;

//Task setup = Task.WhenAll(
//    storageManager.Observable.ForEachAsync(Reporter.Capture)),
//    storageManager.Inf
//    );

//storageManager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
//manager.InfoStream.ForEachAsync(Reporter.CaptureInfo)


public class ZipJsonDocumentSerializer : IJsonDocumentSerializer
{

    public IFieldable Serialize(string rawfield, JObject value)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            using (GZipStream zip = new GZipStream(stream, CompressionLevel.Optimal))
            {
                JsonTextWriter jsonWriter = new JsonTextWriter(new StreamWriter(zip));
                value.WriteTo(jsonWriter);
                jsonWriter.Close();
            }
            byte[] buffer = stream.GetBuffer();
            return new Field(rawfield, buffer, Field.Store.YES);
        }


    }

    public JObject Deserialize(string rawfield, Document document)
    {
        byte[] buffer = document.GetBinaryValue(rawfield);
        using (MemoryStream stream = new MemoryStream(buffer))
        {
            using (GZipStream zip = new GZipStream(stream, CompressionMode.Decompress))
            {
                JsonTextReader reader = new JsonTextReader(new StreamReader(zip));
                JObject entity = (JObject)JToken.ReadFrom(reader);
                reader.Close();
                return entity;
            }
        }
    }
}

public static class Reporter
{
    private static Stopwatch watch = Stopwatch.StartNew();
    private static ConcurrentDictionary<string, long> counters = new();
    private static ConcurrentDictionary<string, GenerationInfo> generations = new();

    public static void Increment(string counter, long currentGen, long latestGen)
    {
        long count = counters.AddOrUpdate(counter, _ => 1, (_, v) => v + 1);
        
        if (count % 25000 != 0) return;
        Console.WriteLine($"{counter} [{watch.Elapsed}] {currentGen:N0} of {latestGen:N0} changes processed, {count:N0} objects indexed. ({count / watch.Elapsed.TotalSeconds:F} / sec)");
    }

    public static void Report()
    {
        Console.WriteLine($"COUNTERS:");
        foreach (var kv in counters)
        {
            var counter = kv.Key;
            var count = kv.Value;
            var gen = generations.GetOrAdd(counter, _ => new GenerationInfo());
            var currentGen = gen.Current;
            var latestGen = gen.Latest;
            Console.WriteLine($"{counter} [{watch.Elapsed}] {currentGen:N0} of {latestGen:N0} changes processed, {count:N0} objects indexed. ({count / watch.Elapsed.TotalSeconds:F} / sec)");
        }
    }

    public static void CaptureInfo(IInfoStreamEvent evt)
    {
        if (evt is StorageObserverInfoStreamEvent sevt)
        {
            switch (sevt.EventType)
            {
                case StorageObserverEventType.Initializing:
                    Console.WriteLine(evt);
                    break;
                case StorageObserverEventType.Initialized:
                    Console.WriteLine(evt);
                    break;
                case StorageObserverEventType.Updating:
                    break;
                case StorageObserverEventType.Updated:
                    break;
                case StorageObserverEventType.Stopped:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

    }

    public static void Capture(IStorageChange change)
    {
        generations.AddOrUpdate(change.Area, 
            _ => change.Generation,
            (_, _) => change.Generation);
        GenerationInfo sum = generations.Values.Aggregate((x, y) => x + y);
        Increment("LOADED", sum.Current, sum.Latest);
        Increment(change.Area, change.Generation.Current, change.Generation.Latest);
    }
    
}
