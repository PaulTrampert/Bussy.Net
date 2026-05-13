using System.Reflection;

namespace Bussy.Net.Helpers;

/// <summary>
/// Extension methods for <see cref="Assembly"/>.
/// </summary>
public static class AssemblyExtensions
{
    /// <summary>
    /// Returns all types that can be loaded from the assembly, silently skipping any types that cause
    /// a <see cref="ReflectionTypeLoadException"/> (for example due to missing dependencies).
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>A sequence of every loadable <see cref="Type"/> in the assembly.</returns>
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