using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using DotJEM.Json.Storage.Adapter;
using Foundation.ObjectHydrator;
using Newtonsoft.Json.Linq;

namespace Stress.Data;

public class StressDataGenerator
{
    public bool stop = false;
    private readonly IStorageArea[] areas;
    private readonly Random random = new Random();
    private readonly RecordGeneratorProvider provider = new RecordGeneratorProvider();

    public async Task StartAsync()
    {
        await Task.WhenAll(areas.Select(GeneratorLoop));
    }

    private  async Task GeneratorLoop(IStorageArea area)
    {
        while (!stop)
        {
            try
            {
                foreach (JObject doc in GenerateAll())
                    area.Insert((string)doc["contentType"], doc);
                await Task.Delay(random.Next(100, 2000));

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    private IEnumerable<JObject> GenerateAll()
    {
        return Generate<Person>()
            .Concat(Generate<Country>())
            .Concat(Generate<City>())
            .Concat(Generate<Game>());
    }

    private IEnumerable<JObject> Generate<T>()
    {
        RecordGenerator<T> generator = provider.Resolve<T>();
        try
        {
            return generator
                .Generate(random.Next(1, 16))
                .Select(x =>
                {
                    JObject json = JObject.FromObject(x);
                    json["contentType"] = typeof(T).Name;
                    return json;
                });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void Stop()
    {
        stop = true;
    }
  

    public StressDataGenerator(params IStorageArea[] areas)
    {
        this.areas = areas;


        provider.Resolve<Person>()
            .WithFirstName(x => x.FirstName)
            .WithLastName(x => x.LastName)
            .WithDate(x => x.BirthDate, new DateTime(1900, 1, 1), DateTime.Today)
            .WithAmericanPhone(x => x.Phone);
        provider.Resolve<Country>();
        provider.Resolve<City>();
        provider.Resolve<Game>();
    }

}


public record Person(string FirstName, string LastName, string Phone, DateTime BirthDate, Gender Gender);
public record Country(string Name, DateTime Founded, Person Leader);
public record City(string Name, long Latitude, long Longitude, Country Country);
public record Game(string Name, string Publisher, DateTime ReleaseDate, long Players);

public enum Gender
{
    Male, Female, Other
}