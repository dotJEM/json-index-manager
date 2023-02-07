using System.Diagnostics;
using System.Reflection;

namespace DotJEM.TaskScheduler;

internal static class ActivitySources
{
    private static readonly string version;

    static ActivitySources()
    {
        Assembly assembly = typeof(ActivitySources).Assembly;
        AssemblyName assemblyName = assembly.GetName();
        version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
                  ?? assemblyName.Version.ToString();
    }

    public static ActivitySource Create<T>()
    {
        return new ActivitySource(typeof(T).FullName!, version);
    }
}