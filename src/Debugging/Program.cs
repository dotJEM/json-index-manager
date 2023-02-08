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
using DotJEM.Json.Index;
using DotJEM.Json.Index.Analyzation;
using DotJEM.Json.Index.Configuration;
using DotJEM.Json.Index.Manager;
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
Task runner = Task.Run(async () => await storageManager.RunAsync());
//Task runner = storage.Run();


storageManager.Observable.ForEachAsync(Reporter.Capture);
storageManager.InfoStream.ForEachAsync(Console.WriteLine);
//storageManager.Observable.LongCount().ForEachAsync(cnt => Console.WriteLine($"Objects loaded: {cnt++}"));

IIndexManager manager = new IndexManager(storageManager, index);

while (true)
{
    switch (Console.ReadLine())
    {
        case "EXIT":
            goto EXIT;
            break;

        default:
            Reporter.Report();
            break;
    }
}

EXIT:
await runner;


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
    private static ConcurrentDictionary<string, Generation> generations = new();

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
            var gen = generations.GetOrAdd(counter, _ => new Generation());
            var currentGen = gen.Current;
            var latestGen = gen.Latest;
            Console.WriteLine($"{counter} [{watch.Elapsed}] {currentGen:N0} of {latestGen:N0} changes processed, {count:N0} objects indexed. ({count / watch.Elapsed.TotalSeconds:F} / sec)");
        }
    }

    public static void Capture(IStorageChange change)
    {
        generations.AddOrUpdate(change.Area, 
            _ => new Generation(change.Generation, change.LatestGeneration),
            (_, _) => new Generation(change.Generation, change.LatestGeneration));
        Generation sum = generations.Values.Aggregate((x, y) => x + y);
        Increment("LOADED", sum.Current, sum.Latest);
        Increment(change.Area, change.Generation, change.LatestGeneration);
    }

    public struct Generation
    {
        public long Current { get; }
        public long Latest { get; }

        public Generation(long current, long latest)
        {
            Current = current;
            Latest = latest;
        }

        public static Generation operator + (Generation left, Generation right)
        {
            return new Generation(left.Current + right.Current, left.Latest + right.Latest);
        }
    }
}
