namespace Perfx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;

    using ColoredConsole;

    using Perfx;

    // https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
    public static class PluginLoader
    {
        public static IPlugin LoadPlugin(Settings settings)
        {
            try
            {
                var plugins = new List<IPlugin>();
                var pluginsDir = "Plugins".GetFullPath();
                if (Directory.Exists(pluginsDir))
                {
                    foreach (var dll in Directory.GetFiles(pluginsDir, "*.dll"))
                    {
                        var pluginAssembly = GetPluginAssembly(dll);
                        var implementations = GetPlugins(pluginAssembly)?.ToList();
                        plugins.AddRange(implementations);
                    }

                    var plugin = plugins.FirstOrDefault(x => x.GetType().FullName.Equals(settings.PluginClassName)) ?? plugins.FirstOrDefault();
                    ColorConsole.WriteLine($"Plugin loaded".DarkGray(), ": ".Green(), (plugin?.GetType()?.FullName ?? "None").DarkGray());
                    return plugin;
                }
            }
            catch (Exception ex)
            {
                ColorConsole.WriteLine(ex.Message.White().OnDarkRed());
            }

            return null;
        }

        private static Assembly GetPluginAssembly(string dllPath)
        {
            var pluginLocation = dllPath.GetFullPath();
            if (pluginLocation == null)
            {
                return null;
            }
            else if (!File.Exists(pluginLocation))
            {
                ColorConsole.WriteLine($"Plugin '{pluginLocation}' does not exist!".OnDarkRed());
                return null;
            }

            var loadContext = new PluginLoadContext(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }

        private static IEnumerable<IPlugin> GetPlugins(Assembly assembly)
        {
            int count = 0;
            if (assembly != null)
            {
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
