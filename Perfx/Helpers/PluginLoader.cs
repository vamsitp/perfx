namespace Perfx.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;

    using ColoredConsole;

    // https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
    public class PluginLoader
    {
        public static List<IPlugin> LoadPlugins(string[] pluginPaths)
        {
            var commands = pluginPaths.SelectMany(pluginPath =>
            {
                var pluginAssembly = LoadPlugin(pluginPath);
                return CreateCommands(pluginAssembly);
            }).ToList();

            return commands;
        }

        internal static Assembly LoadPlugin(string relativePath)
        {
            string pluginLocation = relativePath.GetFullPath();
            ColorConsole.WriteLine($"Loading plugins from: ", pluginLocation.Green());
            var loadContext = new PluginLoadContext(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }

        internal static IEnumerable<IPlugin> CreateCommands(Assembly assembly)
        {
            int count = 0;

            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type))
                {
                    var result = Activator.CreateInstance(type) as IPlugin;
                    if (result != null)
                    {
                        count++;
                        yield return result;
                    }
                }
            }

            if (count == 0)
            {
                string availableTypes = string.Join(",", assembly.GetTypes().Select(t => t.FullName));
                throw new ApplicationException(
                    $"Can't find any type which implements IPlugin in {assembly} from {assembly.Location}.\n" +
                    $"Available types: {availableTypes}");
            }
        }
    }

    internal class PluginLoadContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver resolver;

        public PluginLoadContext(string pluginPath)
        {
            resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
