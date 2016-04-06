using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonVersionModifier
{
    public class Program
    {
        private static Dictionary<string, bool> _packageLookups = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static string[] remove = new[] {"Microsoft.NETCore.Platforms", "NETStandard.Library"};
        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine("Updating versions for: " + Environment.NewLine + string.Join(Environment.NewLine, args));
                Console.WriteLine();

                foreach (var fileName in args)
                {
                    var projectName = Path.GetDirectoryName(fileName).Split('/', '\\').Last();
                    JObject data;
                    var updateFile = false;

                    Console.WriteLine($"Updating {projectName} project.json");
                    using (var file = File.OpenText(fileName))
                    using (var reader = new JsonTextReader(file))
                    {
                        try
                        {
                            data = (JObject)JToken.ReadFrom(reader);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Invalid project.json" + ex);
                            return;
                        }

                        updateFile |= UpdateDependencies(data, false);
                        updateFile |= UpdateFrameworks(data);
                        updateFile |= AddRuntimes(data);
                    }

                    if (updateFile)
                    {
                        SaveDataTo(fileName, data);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
                Debugger.Launch();
            }
        }

        private static bool UpdateDependencies(JToken data, bool addCore)
        {
            var dependencies = data["dependencies"] as JObject;
            if (dependencies == null)
            {
                return false;
            }

            var updateFile = false;
            if (addCore)
            {
                updateFile = true;
                dependencies.AddFirst(new JProperty("Microsoft.NETCore.App",
                        new JObject(
                            new JProperty("version", "1.0.0-*"),
                            new JProperty("type", "platform"))));
            }
            foreach (var dependency in dependencies.Properties().ToArray())
            {
                if (remove.Contains(dependency.Name))
                {
                    dependency.Remove();
                }
            }

            return updateFile;
        }

        private static bool UpdateFrameworks(JToken data)
        {
            var frameworks = data["frameworks"] as JObject;
            var updateFile = false;
            foreach (var framework in frameworks.Properties().ToArray())
            {
                if (framework.Name.StartsWith("netstandard"))
                {
                    UpdateDependencies(framework.Value, true);
                    framework.Replace(new JProperty("netcoreapp1.0", framework.Value));
                    updateFile = true;
                }
            }
            return updateFile;
        }

        private static bool AddRuntimes(JToken data)
        {
            var frameworks = data["frameworks"] as JObject;

            var updateFile = false;
            if (frameworks.Properties().Any(p => p.Name.StartsWith("net")))
            {
                var runtimes = data["runtimes"] as JObject;
                if (runtimes == null)
                {
                    data["runtimes"] = (runtimes = new JObject());
                }
                runtimes["win7-x64"] = new JObject();
                runtimes["win7-x86"] = new JObject();
                updateFile = true;
            }
            return updateFile;
        }

        private static void SaveDataTo(string fileName, object data)
        {
            using (var fileStream = File.Open(fileName, FileMode.Truncate))
            using (var streamWriter = new StreamWriter(fileStream))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                var serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, data);
            }
        }
    }
}
