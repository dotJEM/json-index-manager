﻿// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index;
using DotJEM.Json.Index.Configuration;
using DotJEM.Json.Index.Manager;
using DotJEM.Json.Index.Manager.Configuration;
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Index.Manager.Snapshots.Zip;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Configuration;
using DotJEM.TaskScheduler;
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


//IStorageManager storageManager = new StorageManager(
//    storage,
//    new WebBackgroundTaskScheduler(),
//    new DefaultStorageWatchConfiguration());
//IIndexManager manager = new IndexManager(
//    storageManager, 
//    new IndexSnapshotManager(new ZipSnapshotStrategy(".\\app_data\\snapshots")),
//    new WriteContextFactory(index));

Directory.Delete(".\\app_data\\index", true);
Directory.CreateDirectory(".\\app_data\\index");

IIndexManager indexManager = new IndexManager(
    storage,
    index,
    new ZipSnapshotStrategy(".\\app_data\\snapshots"),
    new WebBackgroundTaskScheduler(),
    new DefaultIndexManagerConfiguration()
);
Task run = Task.WhenAll(
    //storageManager.Observable.ForEachAsync(Reporter.Capture),
    //storageManager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
    //manager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
    indexManager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
    Task.Run(indexManager.RunAsync)
);


while (true)
{
    switch (Console.ReadLine()?.ToUpper().FirstOrDefault())
    {
        case 'E':
        case 'Q':
            goto EXIT;

        case 'S':
            await indexManager.TakeSnapshotAsync();
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
    private static ITrackerState lastState;
 
    public static void CaptureInfo(IInfoStreamEvent evt)
    {
        switch (evt)
        {
            case StorageObserverInfoStreamEvent sevt:
                switch (sevt.EventType)
                {
                    case StorageObserverEventType.Starting:
                    case StorageObserverEventType.Initializing:
                    case StorageObserverEventType.Initialized:
                        Console.WriteLine(evt.Message);
                        break;
                    case StorageObserverEventType.Updating:
                    case StorageObserverEventType.Updated:
                    case StorageObserverEventType.Stopped:
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
