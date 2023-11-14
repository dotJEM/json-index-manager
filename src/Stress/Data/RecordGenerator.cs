using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Foundation.ObjectHydrator;
using Foundation.ObjectHydrator.Generators;
using Foundation.ObjectHydrator.Interfaces;

namespace Stress.Data;

public interface IGeneratorProvider
{
    IGenerator<T> Resolve<T>();
    IGenerator<object> Resolve(Type type);
}
public class RecordGeneratorProvider : IGeneratorProvider
{
    private readonly IDefaultParameterMappings defaults;
    private readonly IDictionary<Type, IRecordGenerator> generators = new Dictionary<Type, IRecordGenerator>();
    public RecordGeneratorProvider(IDefaultParameterMappings defaults = null)
    {
        this.defaults = defaults ?? new DefaultParameterMappings(this);
    }

    public RecordGenerator<T> Resolve<T>()
    {
        Type key = typeof(T);
        if (!generators.TryGetValue(key, out IRecordGenerator generator))
            generators[typeof(T)] = generator = new RecordGenerator<T>(defaults);
        return (RecordGenerator<T>)generator;
    }

    public IGenerator<object> Resolve(Type type)
    {
        if (!generators.TryGetValue(type, out IRecordGenerator generator))
        {
            ConstructorInfo ctor = typeof(RecordGenerator<>).MakeGenericType(type).GetConstructor(new[] { typeof(IDefaultParameterMappings)});
            //generator = (IRecordGenerator)Activator.CreateInstance(generatorType, defaults);
            generator = (IRecordGenerator)ctor.Invoke(new object[] { defaults });
            generators[type] = generator;
        }

        return new Wrapper(generator);
    }

    IGenerator<T> IGeneratorProvider.Resolve<T>()
        => this.Resolve<T>();

    private class Wrapper : IGenerator<object>
    {
        private readonly IRecordGenerator generator;

        public Wrapper(IRecordGenerator generator)
        {
            this.generator = generator;
        }

        public object Generate()
        {
            return generator.Generate();
        }
    }
}

public interface IRecordGenerator
{
    object Generate();
}

public class RecordGenerator<T> : IGenerator<T>, IRecordGenerator
{
    private readonly ConstructorInfo ctor;
    private readonly ParameterInfo[] arguments;
    private readonly Dictionary<string, IParameterMapping> argumentsMap = new();
    private readonly IDefaultParameterMappings defaults;

    public RecordGenerator(IDefaultParameterMappings defaults)
        : this(ctors => ctors.First(), defaults) { }

    public RecordGenerator(Func<ConstructorInfo[], ConstructorInfo> ctorSelector, IDefaultParameterMappings defaults)
        : this(ctorSelector(typeof(T).GetConstructors()),defaults) {}

    public RecordGenerator(ConstructorInfo ctor, IDefaultParameterMappings defaults)
    {
        this.ctor = ctor;
        this.defaults = defaults;
        this.arguments = ctor.GetParameters();
        this.argumentsMap = ctor
            .GetParameters()
            .ToDictionary(param => param.Name, CreateDefaultMapping);
    }

    private IParameterMapping CreateDefaultMapping(ParameterInfo arg)
    {
        IParameterMap match = defaults.Find(map => map.Match(arg));
        return match.Mapping(arg);
    }

    public IEnumerable<T> Generate(int count)
    {
        return Enumerable.Range(0, count).Select(x => Generate());
    }

    public T Generate()
    {
        object[] args = arguments.Select(
            arg => argumentsMap[arg.Name].Generate()
        ).ToArray();
        return (T)ctor.Invoke(args);
    }
    

    public RecordGenerator<T> WithFirstName<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        IGenerator<TProperty> gen = (IGenerator<TProperty>)new FirstNameGenerator();
        SetArgumentMap(expression, gen);
        return this;
    }

    public RecordGenerator<T> WithLastName<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        IGenerator<TProperty> gen = (IGenerator<TProperty>)new LastNameGenerator();
        SetArgumentMap(expression, gen);
        return this;
    }

    public RecordGenerator<T> WithDate<TProperty>(Expression<Func<T, TProperty>> expression, DateTime minimum, DateTime maximum)
    {

        IGenerator<TProperty> gen = (IGenerator<TProperty>)new DateTimeGenerator(minimum, maximum);
        SetArgumentMap(expression, gen);
        return this;
    }

    public RecordGenerator<T> WithAmericanPhone<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        IGenerator<TProperty> gen = (IGenerator<TProperty>)new AmericanPhoneGenerator();
        SetArgumentMap(expression, gen);
        return this;
    }

    private void SetArgumentMap<TProperty>(Expression<Func<T, TProperty>> expression, IGenerator<TProperty> generator)
    {
        string propertyName = ((MemberExpression)expression.Body).Member.Name;
        PropertyInfo propertyInfo = typeof(T).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        argumentsMap[propertyInfo.Name] = new ParameterMapping<TProperty>(propertyInfo.Name, generator);
    }

    object IRecordGenerator.Generate()
    {
        return Generate();
    }
}

public static class GeneratorExt
{
    public static IEnumerable<T> Generate<T>(this IGenerator<T> self, int count)
        => Enumerable.Repeat(0, count).Select(_ => self.Generate());
}

public interface IParameterMapping
{
    string ArgumentName { get; }

    object Generate();
}

public class ParameterMapping<T> : IParameterMapping
{
    private readonly IGenerator<T> generator;

    public string ArgumentName { get;  }
  
    public ParameterMapping(string name, IGenerator<T> generator)
    {
        this.generator = generator;
        this.ArgumentName = name;
    }
    
    public object Generate() => generator.Generate();
}

public interface IDefaultParameterMappings
{
    IParameterMap Find(Predicate<IParameterMap> predicate);
}

public class DefaultParameterMappings:List<IParameterMap>, IDefaultParameterMappings
{
        public DefaultParameterMappings(IGeneratorProvider typeGeneratorProvider)
        {
            Add(new ParameterMap<DateTime>().Using(new DateTimeGenerator()));
            Add(new ParameterMap<double>().Using(new DoubleGenerator()));
            Add(new ParameterMap<int>().Using(new IntegerGenerator()));
            Add(new ParameterMap<long>().Using(new Int64Generator()));
            Add(new ParameterMap<bool>().Using(new BooleanGenerator()));
            Add(new ParameterMap<Guid>().Using(new GuidGenerator()));
            Add(new ParameterMap<byte[]>().Using(new ByteArrayGenerator(8)));
            Add(new EnumParameterMap());
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower() == "firstname" || info.Name.ToLower() == "fname")
                    .Using(new FirstNameGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower() == "lastname" || info.Name.ToLower() == "lname")
                    .Using(new LastNameGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower().Contains("email") && info.ParameterType == typeof(string))
                    .Using(new EmailAddressGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower().Contains("password") && info.ParameterType == typeof(string))
                    .Using(new PasswordGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower().Contains("trackingnumber"))
                    .Using(new TrackingNumberGenerator("ups")));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower() == "ipaddress")
                    .Using(new IPAddressGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower().Contains("country") && info.ParameterType == typeof(string))
                    .Using(new CountryCodeGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower() == "gender" && info.ParameterType == typeof(string))
                    .Using(new GenderGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower() == "creditcardtype")
                    .Using(new CreditCardTypeGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower().Contains("addressline") || info.Name.ToLower().Contains("address"))
                    .Using(new AmericanAddressGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower().Contains("creditcard") ||
                              info.Name.ToLower().Contains("cardnum") ||
                              info.Name.ToLower().Contains("ccnumber"))
                    .Using(new CreditCardNumberGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower().Contains("url") ||
                              info.Name.ToLower().Contains("website") ||
                              info.Name.ToLower().Contains("homepage"))
                    .Using(new WebsiteGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower() == "city")
                    .Using(new AmericanCityGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower() == "state")
                    .Using(new AmericanStateGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower() == "company" ||
                              info.Name.ToLower() == "business" ||
                              info.Name.ToLower() == "companyname")
                    .Using(new CompanyNameGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower()
                    .Contains("description")).Using(new TextGenerator(25)));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower().Contains("phone")).Using(
                    new AmericanPhoneGenerator()));
            Add(new ParameterMap<string>()
                    .Matching(info => info.Name.ToLower().Contains("zip") || info.Name.ToLower().Contains("postal"))
                    .Using(new AmericanPostalCodeGenerator(25)));
            Add(new ParameterMap<string>().Using(new TextGenerator(50)));
            Add(new RecordParameterMap(typeGeneratorProvider));
        }
    }
public interface IParameterMap
{
    bool Match(ParameterInfo info);
    IParameterMapping Mapping(ParameterInfo info);
}  
public class ParameterMap<T>:IParameterMap
{
    private IGenerator<T> generator;
    private Func<ParameterInfo, bool> func = info => info.ParameterType == typeof(T);

    bool IParameterMap.Match(ParameterInfo info) => func(info);

    IParameterMapping IParameterMap.Mapping(ParameterInfo info)
    {
        return new ParameterMapping<T>(info.Name, generator);
    }

    public ParameterMap<T> Matching(Func<ParameterInfo, bool> func)
    {
        this.func = func;
        return this;
    }

    public ParameterMap<T> Using(IGenerator<T> generator)
    {
        this.generator = generator;
        return this;
    }

    public ParameterMap<T> Using(T defaultValue) => Using(new DefaultGenerator<T>(defaultValue));
}

public class RecordParameterMap : IParameterMap
{
    private readonly IGeneratorProvider generatorProvider;

    public RecordParameterMap(IGeneratorProvider generatorProvider)
    {
        this.generatorProvider = generatorProvider;
    }

    bool IParameterMap.Match(ParameterInfo info)
    {
        return info.ParameterType.IsClass || info.ParameterType is { IsValueType: true, IsPrimitive: false };
    }

    IParameterMapping IParameterMap.Mapping(ParameterInfo info)
        => new ParameterMapping<object>(info.Name,
            generatorProvider.Resolve(info.ParameterType));
}
public class EnumParameterMap : IParameterMap
{

    bool IParameterMap.Match(ParameterInfo info)
        => info.ParameterType.IsEnum;

    IParameterMapping IParameterMap.Mapping(ParameterInfo info) 
        => new ParameterMapping<object>(info.Name, new EnumGenerator(Enum.GetValues(info.ParameterType)));
} 

public class Int64Generator : IGenerator<long>
{
    private readonly Random random;

    public int MinimumValue { get; set; }

    public int MaximumValue { get; set; }

    public Int64Generator()
        : this(0, 100)
    {
    }

    public Int64Generator(int minimumValue, int maximumValue)
    {
        this.MinimumValue = minimumValue;
        this.MaximumValue = maximumValue;
        this.random = RandomSingleton.Instance.Random;
    }

    public long Generate() => this.random.Next(this.MinimumValue, this.MaximumValue + 1);
}