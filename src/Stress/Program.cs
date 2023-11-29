// See https://aka.ms/new-console-template for more information

using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DotJEM.Json.Index.Manager;
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Index.Manager.Snapshots.Zip;
using DotJEM.Json.Index.Manager.Snapshots.Zip.Meta;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Index.Manager.Writer;
using DotJEM.Json.Index2;
using DotJEM.Json.Index2.Documents.Fields;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.Json.Index2.Storage;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Configuration;
using DotJEM.ObservableExtensions.InfoStreams;
using DotJEM.Web.Scheduler;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Util;
using Stress.Adapter;
using Stress.Data;

//TraceSource trace; 

IStorageContext storage = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=SSN3DB;Integrated Security=True");
storage.Configure.MapField(JsonField.Id, "id");
storage.Configure.MapField(JsonField.ContentType, "contentType");
storage.Configure.MapField(JsonField.Version, "$version");
storage.Configure.MapField(JsonField.Created, "$created");
storage.Configure.MapField(JsonField.Updated, "$updated");
storage.Configure.MapField(JsonField.SchemaVersion, "$schemaVersion");

//StressDataGenerator generator = new StressDataGenerator(
//    storage.Area(),
//    storage.Area("Settings"),
//    storage.Area("Queue"),
//    storage.Area("Recipes"),
//    storage.Area("Animals"),
//    storage.Area("Games"),
//    storage.Area("Players"),
//    storage.Area("Planets"),
//    storage.Area("Universe"),
//    storage.Area("Trashcan")
//);
//Task genTask = generator.StartAsync();
//await Task.Delay(5000);

Directory.CreateDirectory(".\\app_data\\index");

IJsonIndex index = new JsonIndexBuilder("main")
    .UsingSimpleFileStorage(".\\app_data\\index")
    .WithAnalyzer(cfg=> new StandardAnalyzer(cfg.Version,CharArraySet.EMPTY_SET))
    .WithFieldResolver(new FieldResolver("id", "contentType"))
    .WithSnapshoting()
    .Build();

//IStorageIndex index = new LuceneStorageIndex(new LuceneFileIndexStorage(".\\app_data\\index",);
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
    new JsonIndexSnapshotManager(index, new ZipSnapshotStrategy(".\\app_data\\snapshots"), scheduler, "30m"),
    new JsonIndexWriter(index, scheduler)
);


Task run = Task.WhenAll(
    jsonIndexManager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
    jsonIndexManager.RunAsync()
    //genTask
);


while (true)
{
    switch (Console.ReadLine()?.ToUpper().FirstOrDefault())
    {
        case 'E':
        case 'Q':
            //generator.Stop();
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


public static class Reporter
{
    private static ITrackerState lastState;
    private static IInfoStreamEvent lastEvent;
    private static DateTime lastReport = DateTime.Now;

    public static void CaptureInfo(IInfoStreamEvent evt)
    {
        switch (evt)
        {
            case TrackerStateInfoStreamEvent ievt :
                lastState = ievt.State;
                break;

            default:
                lastEvent = evt;
                break;
        }
        Report();
    }

    private static string CleanLine = new string(' ', Console.BufferWidth);
    public static void Report()
    {
        if(DateTime.Now - lastReport < TimeSpan.FromSeconds(2))
            return;

        lastReport = DateTime.Now;
        Console.SetCursorPosition(0,0);
        for (int i = 0; i < 20; i++)
            Console.WriteLine(CleanLine);                                                                            
        Console.SetCursorPosition(0,0);

        Console.WriteLine(lastEvent.Message);
        Console.WriteLine(lastState);
    }
}
