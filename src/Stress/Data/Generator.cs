using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using DotJEM.Json.Storage.Adapter;
using Foundation.ObjectHydrator;
using Foundation.ObjectHydrator.Interfaces;
using Foundation.ObjectHydrator.Generators;
using Newtonsoft.Json.Linq;

namespace Stress.Data;

public class StressDataGenerator
{
    public bool stop = false;
    private readonly IStorageArea[] areas;
    private readonly Random random = new Random();
    private readonly Dictionary<Type, object> generators = new Dictionary<Type, object>();

    public async Task StartAsync()
    {
        await Task.WhenAll(areas.Select(GeneratorLoop));
    }

    private  async Task GeneratorLoop(IStorageArea area)
    {
        while (!stop)
        {
            foreach (JObject doc in GenerateAll())
                area.Insert((string)doc["contentType"], doc);
            await Task.Delay(random.Next(100, 2000));
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
        Hydrator<T> generator;
        if(!generators.TryGetValue(typeof(T), out object untyped))
            generators.Add(typeof(T), generator = new Hydrator<T>());
        else
            generator = (Hydrator<T>)untyped;
        try
        {
            
            return generator
                .GetList(random.Next(1, 16))
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


        Add(new Hydrator<Person>()
            .WithFirstName(x => x.FirstName)
            .WithLastName(x => x.LastName)
            .WithDate(x => x.BirthDate, new DateTime(1900, 1, 1), DateTime.Today)
            .WithAmericanPhone(x => x.Phone)
        );
    }

    private void Add<T>(Hydrator<T> generator) => generators.Add(typeof(T), generator);
}


public record Person(string FirstName, string LastName, string Phone, DateTime BirthDate, Gender Gender);
public record Country(string Name, DateTime Founded, Person Leader);
public record City(string Name, long Latitude, long Longitude, Country Country);
public record Game(string Name, string Publisher, DateTime ReleaseDate, long Players);

public enum Gender
{
    Male, Female, Other
}

public class RecordGenerator<T> : IGenerator<T>
{
    private readonly ConstructorInfo ctor;
    private readonly ParameterInfo[] arguments;

    public RecordGenerator()
        : this(ctors => ctors.First()) { }

    public RecordGenerator(Func<ConstructorInfo[], ConstructorInfo> ctorSelector)
        : this(ctorSelector(typeof(T).GetConstructors())) {}

    public RecordGenerator(ConstructorInfo ctor)
    {
        this.ctor = ctor;
        this.arguments = ctor.GetParameters();
    }
    
    public IEnumerable<T> Generate(int count)
    {
        return Enumerable.Range(0, count).Select(x => Generate());
    }

    public T Generate()
    {
        throw new NotImplementedException();
    }
}