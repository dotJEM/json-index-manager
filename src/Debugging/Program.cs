// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using Lucene.Net.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Version = Lucene.Net.Util.Version;



var storage = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=ssn3db;Integrated Security=True");
storage.Configure.MapField(JsonField.Id, "id");
storage.Configure.MapField(JsonField.ContentType, "contentType");
storage.Configure.MapField(JsonField.Version, "$version");
storage.Configure.MapField(JsonField.Created, "$created");
storage.Configure.MapField(JsonField.Updated, "$updated");
storage.Configure.MapField(JsonField.SchemaVersion, "$schemaVersion");

var index = new LuceneStorageIndex(new LuceneFileIndexStorage(".\\app_data\\index", new DotJemAnalyzer(Version.LUCENE_30)));
index.Configuration.SetTypeResolver("contentType");
index.Configuration.SetRawField("$raw");
index.Configuration.SetScoreField("$score");
index.Configuration.SetIdentity("id");
index.Configuration.SetSerializer(new ZipJsonDocumentSerializer());


IStorageManager storageManager = new StorageManager(storage, new DotJEM.TaskScheduler.TaskScheduler());
Task runner = Task.Run(async () => await storageManager.RunAsync());
//Task runner = storage.Run();
storageManager.Observable
    .ForEachAsync(_ =>
    {
        Reporter.Increment("LOAD");
    });



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
    private static long exceptions;
    private static Stopwatch watch = Stopwatch.StartNew();
    private static ConcurrentDictionary<string, long> counters = new();

    public static void IncrementExceptions()
    {
        Interlocked.Increment(ref exceptions);

    }

    public static void Increment(string counter)
    {
        long count = counters.AddOrUpdate(counter, _ => 1, (_, v) => v + 1);
        if(count % 25000 == 0) Console.WriteLine($"{counter} [{watch.Elapsed}] {count} ({count / watch.Elapsed.TotalSeconds} / sec) => {exceptions}");
    }

    public static void Report()
    {
        Console.WriteLine($"COUNTERS:");
        foreach (var counter in counters)
        {
            Console.WriteLine($"{counter.Key} [{watch.Elapsed}] {counter.Value} ({counter.Value / watch.Elapsed.TotalSeconds} / sec) => {exceptions}");
        }
    }
}
