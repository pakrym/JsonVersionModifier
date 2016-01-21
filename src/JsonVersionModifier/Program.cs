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
        private static readonly HashSet<string> PostRTMPackages = new HashSet<string>(StringComparer.Ordinal)
        {
            "Microsoft.AspNetCore.Authentication.OpenIdConnect",
            "Microsoft.AspNetCore.Buffering",
            "Microsoft.AspNetCore.Diagnostics.Elm",
            "Microsoft.AspNetCore.HttpOverrides",
            "Microsoft.AspNetCore.Proxy",
            "Microsoft.AspNetCore.Server.WebListener",
            "Microsoft.AspNetCore.SignalR.Messaging",
            "Microsoft.AspNetCore.SignalR.Redis",
            "Microsoft.AspNetCore.SignalR.Server",
            "Microsoft.AspNetCore.SignalR.Sources",
            "Microsoft.AspNetCore.SignalR.SqlServer",
            "Microsoft.AspNetCore.SignalR.ServiceBus",
            "Microsoft.AspNetCore.WebSockets.Client",
            "Microsoft.AspNetCore.WebSockets.Protocol",
            "Microsoft.AspNetCore.WebSockets.Server",
            "Microsoft.Net.Http.Server",
            "Microsoft.Net.WebSockets.Server",
        };
        private static Dictionary<string, bool> _packageLookups = new Dictionary<string, bool>(StringComparer.Ordinal);

        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting Version Updates...");

                using (var file = File.OpenText("C:/Users/nimullen/Documents/GitHub/JsonVersionModifier/src/JsonVersionModifier/packageLookups.json"))
                using (var reader = new JsonTextReader(file))
                {
                    var serializer = new JsonSerializer();
                    _packageLookups = serializer.Deserialize<Dictionary<string, bool>>(reader);
                }
                Console.WriteLine("Package Lookup cache loaded.");

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
                        catch
                        {
                            Console.WriteLine("Invalid project.json");
                            return;
                        }

                        var projectVersion = data["version"];

                        if (projectVersion != null)
                        {
                            var projectVersionValue = projectVersion.Value<string>();

                            if (PostRTMPackages.Contains(projectName))
                            {
                                updateFile = true;
                                data["version"] = "0.1.0-*";
                            }
                            else if (!projectVersionValue.StartsWith("1.0.0", StringComparison.Ordinal))
                            {
                                if (projectName.Contains("AspNetCore") ||
                                    _packageLookups.ContainsKey(projectName) &&
                                    _packageLookups[projectName])
                                {
                                    updateFile = true;
                                    data["version"] = "1.0.0-*";
                                }
                                else if (!_packageLookups.ContainsKey(projectName))
                                {
                                    Console.WriteLine(projectName + ": " + projectVersionValue);
                                    Console.WriteLine("Force to 1.0.0-*?");

                                    var result = Console.ReadLine();
                                    if (result[0] == 'y' || result[0] == 'Y')
                                    {
                                        updateFile = true;
                                        _packageLookups[projectName] = true;
                                        data["version"] = "1.0.0-*";
                                    }
                                    else
                                    {
                                        _packageLookups[projectName] = false;
                                    }
                                }
                            }
                        }

                        if (data["dependencies"] != null && data["dependencies"] is JObject)
                        {
                            updateFile = UpdateChildVersions(((JObject)data["dependencies"]).Properties()) || updateFile;
                        }

                        var children = data["frameworks"].Children();
                        foreach (var framework in children)
                        {
                            var frameworkDependencies = framework.Values("dependencies");
                            if (frameworkDependencies != null)
                            {
                                updateFile = UpdateChildVersions(frameworkDependencies.Values<JProperty>()) || updateFile;
                            }
                        }
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
            finally
            {
                SaveDataTo("C:/Users/nimullen/Documents/GitHub/JsonVersionModifier/src/JsonVersionModifier/packageLookups.json", _packageLookups);
            }
        }

        private static bool UpdateChildVersions(IEnumerable<JProperty> dependencies)
        {
            var updateFile = false;
            foreach (var dependency in dependencies)
            {
                if (PostRTMPackages.Contains(dependency.Name))
                {
                    SetVersion("0.1.0-*", dependency);
                    updateFile = true;
                }
                else
                {
                    var version = GetVersion(dependency);

                    if (dependency.Name.StartsWith("System", StringComparison.Ordinal) ||
                        version.StartsWith("1.0.0", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    else if (dependency.Name.Contains("AspNetCore") ||
                        _packageLookups.ContainsKey(dependency.Name) &&
                        _packageLookups[dependency.Name])
                    {
                        updateFile = true;
                        SetVersion("1.0.0-*", dependency);
                    }
                    else if (!_packageLookups.ContainsKey(dependency.Name))
                    {
                        Console.WriteLine(dependency.Name + ": " + version);
                        Console.WriteLine("Force to 1.0.0-*");

                        var result = Console.ReadLine();
                        if (result[0] == 'y' || result[0] == 'Y')
                        {
                            Console.WriteLine("Forcing to 1.0.0.");
                            updateFile = true;
                            _packageLookups[dependency.Name] = true;
                            SetVersion("1.0.0-*", dependency);
                        }
                        else
                        {
                            _packageLookups[dependency.Name] = false;
                        }
                    }
                }
            }

            return updateFile;
        }

        private static void SetVersion(string version, JProperty dependency)
        {
            var child = dependency.Children().First();
            if (child.HasValues)
            {
                child["version"] = version;
            }
            else
            {
                dependency.Value = version;
            }
        }

        private static string GetVersion(JProperty dependency)
        {
            var child = dependency.Children().First();

            if (child.HasValues && child["version"] != null)
            {
                return child["version"].Value<string>();
            }
            else
            {
                try
                {
                    return child.Value<string>();
                }
                catch
                {
                    return string.Empty;
                }
            }
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
