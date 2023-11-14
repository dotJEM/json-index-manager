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
using DotJEM.Json.Index.Manager;
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Index.Manager.Snapshots.Zip;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Index.Manager.V1Adapter;
using DotJEM.Json.Index.Manager.Writer;
using DotJEM.Json.Index2;
using DotJEM.Json.Index2.Documents.Fields;
using DotJEM.Json.Index2.Storage;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Configuration;
using DotJEM.ObservableExtensions.InfoStreams;
using DotJEM.Web.Scheduler;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

//TraceSource trace; 

IStorageContext storage = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=ssn3db;Integrated Security=True");
storage.Configure.MapField(JsonField.Id, "id");
storage.Configure.MapField(JsonField.ContentType, "contentType");
storage.Configure.MapField(JsonField.Version, "$version");
storage.Configure.MapField(JsonField.Created, "$created");
storage.Configure.MapField(JsonField.Updated, "$updated");
storage.Configure.MapField(JsonField.SchemaVersion, "$schemaVersion");

IJsonIndex index = new JsonIndexBuilder("foo")
    .UsingStorage(new SimpleFsJsonIndexStorage(".\\app_data\\index"))
    .WithAnalyzer(configuration => new StandardAnalyzer(configuration.Version, CharArraySet.EMPTY_SET))
    .WithFieldResolver(new FieldResolver("id", "contentType"))
    .Build();

//IStorageIndex index = new LuceneStorageIndex(new LuceneFileIndexStorage(".\\app_data\\index", new StandardAnalyzer(LuceneVersion.LUCENE_48, CharArraySet.EMPTY_SET)));
//index.Configuration.SetTypeResolver("contentType");
//index.Configuration.SetRawField("$raw");
//index.Configuration.SetScoreField("$score");
//index.Configuration.SetIdentity("id");
//index.Configuration.SetSerializer(new ZipJsonDocumentSerializer());

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
