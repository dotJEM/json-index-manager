// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Debugging.Adapter;
using DotJEM.Json.Index;
using DotJEM.Json.Index.Configuration;
using DotJEM.Json.Index.Manager;
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Index.Manager.Snapshots.Zip;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Index.Manager.Writer;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Configuration;
using DotJEM.ObservableExtensions.InfoStreams;
using DotJEM.Web.Scheduler;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Version = Lucene.Net.Util.Version;

//TraceSource trace; 

IStorageContext storage = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=ssn3db;Integrated Security=True");
storage.Configure.MapField(JsonField.Id, "id");
storage.Configure.MapField(JsonField.ContentType, "contentType");
storage.Configure.MapField(JsonField.Version, "$version");
storage.Configure.MapField(JsonField.Created, "$created");
storage.Configure.MapField(JsonField.Updated, "$updated");
storage.Configure.MapField(JsonField.SchemaVersion, "$schemaVersion");

IStorageIndex index = new LuceneStorageIndex(new LuceneFileIndexStorage(".\\app_data\\index", new StandardAnalyzer(Version.LUCENE_30, new SortedSet<string>())));
index.Configuration.SetTypeResolver("contentType");
index.Configuration.SetRawField("$raw");
index.Configuration.SetScoreField("$score");
index.Configuration.SetIdentity("id");
index.Configuration.SetSerializer(new ZipJsonDocumentSerializer());

Directory.Delete(".\\app_data\\index", true);
Directory.CreateDirectory(".\\app_data\\index");

IWebTaskScheduler scheduler = new WebTaskScheduler();
IJsonIndexManager jsonIndexManager = new JsonIndexManager(
    new JsonStorageDocumentSource(new JsonStorageAreaObserverFactory(storage, scheduler)),
    new JsonIndexSnapshotManager(index, new ZipSnapshotStrategy(".\\app_data\\snapshots", 2), scheduler, "10m"),
    new JsonIndexWriter(index, scheduler)
);


Task run = Task.WhenAll(
    jsonIndexManager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
    jsonIndexManager.RunAsync()
);


while (true)
{
    switch (Console.ReadLine()?.ToUpper().FirstOrDefault())
    {
        case 'E':
        case 'Q':
            goto EXIT;

        case 'S':
            await jsonIndexManager.TakeSnapshotAsync();
            break;

        case 'C':
            index.Commit();
            break;

        case 'O':
            index.Optimize();
            break;

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
        using MemoryStream stream = new();
        using (GZipStream zip = new(stream, CompressionLevel.Optimal))
        {
            JsonTextWriter jsonWriter = new(new StreamWriter(zip));
            value.WriteTo(jsonWriter);
            jsonWriter.Close();
        }
        byte[] buffer = stream.GetBuffer();
        return new Field(rawfield, buffer, Field.Store.YES);
    }

    public JObject Deserialize(string rawfield, Document document)
    {
        byte[] buffer = document.GetBinaryValue(rawfield);
        using MemoryStream stream = new(buffer);
        using GZipStream zip = new(stream, CompressionMode.Decompress);
        JsonTextReader reader = new(new StreamReader(zip));
        JObject entity = (JObject)JToken.ReadFrom(reader);
        reader.Close();
        return entity;
    }
}

public static class Reporter
{
    private static ITrackerState lastState;
 
    public static void CaptureInfo(IInfoStreamEvent evt)
    {
        switch (evt)
        {
            case StorageObserverInfoStreamEvent sevt:
                switch (sevt.EventType)
                {
                    case JsonSourceEventType.Starting:
                    case JsonSourceEventType.Initializing:
                    case JsonSourceEventType.Initialized:
                        Console.WriteLine(evt.Message);
                        break;
                    case JsonSourceEventType.Updating:
                    case JsonSourceEventType.Updated:
                    case JsonSourceEventType.Stopped:
                        break;
                }
                break;

            case TrackerStateInfoStreamEvent ievt :
                lastState = ievt.State;
                break;

            default:
                Console.WriteLine(evt.Message);
                break;
        }
    }


    public static void Report()
    {
        Console.WriteLine(lastState);

    }
}
