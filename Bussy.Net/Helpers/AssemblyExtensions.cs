using System.Reflection;

namespace Bussy.Net.Helpers;

public static class AssemblyExtensions
{
    public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null).Cast<Type>();
        }
    }
}