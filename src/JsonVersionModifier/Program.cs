using System;
using System.Collections.Generic;
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
            "Microsoft.AspNet.Authentication.OpenIdConnect",
            "Microsoft.AspNet.Buffering",
            "Microsoft.AspNet.Diagnostics.Elm",
            "Microsoft.AspNet.HttpOverrides",
            "Microsoft.AspNet.Proxy",
            "Microsoft.AspNet.Server.WebListener",
            "Microsoft.AspNet.SignalR.Messaging",
            "Microsoft.AspNet.SignalR.Redis",
            "Microsoft.AspNet.SignalR.Server",
            "Microsoft.AspNet.SignalR.Sources",
            "Microsoft.AspNet.SignalR.SqlServer",
            "Microsoft.AspNet.WebSockets.Client",
            "Microsoft.AspNet.WebSockets.Protocol",
            "Microsoft.AspNet.WebSockets.Server",
        };

        public static void Main(string[] args)
        {
            foreach (var fileName in args)
            {
                var projectName = Path.GetDirectoryName(fileName).Split('/', '\\').Last();
                using (var file = File.OpenText(fileName))
                using (var reader = new JsonTextReader(file))
                {
                    var data = (JObject)JToken.ReadFrom(reader);
                    var newVersion = PostRTMPackages.Contains(projectName) ? "0.1-*" : "1.0-*";
                    data["version"] = newVersion;

                    foreach (var dependency in ((JObject)data["dependencies"]).Properties())
                    {
                        if (PostRTMPackages.Contains(dependency.Name))
                        {
                            SetVersion("0.1.0-*", dependency);
                        }
                        else
                        {
                            var version = GetVersion(dependency);

                            if (dependency.Name.StartsWith("System", StringComparison.Ordinal) ||
                                version.StartsWith("1.0.0", StringComparison.Ordinal))
                            {
                                continue;
                            }
                            else if (dependency.Name.Contains("AspNet"))
                            {
                                SetVersion("1.0.0-*", dependency);
                            }
                            else
                            {
                                Console.WriteLine(dependency.Name + ": " + version);
                                Console.WriteLine("Force to 1.0.0?");
                                var result = Console.ReadLine();
                                if (result[0] == 'y' || result[0] == 'Y')
                                {
                                    SetVersion("1.0.0", dependency);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void SetVersion(string version, JProperty dependency)
        {
            if (dependency.HasValues)
            {
                dependency["version"] = version;
            }
            else
            {
                dependency.Value = version;
            }
        }

        private static string GetVersion(JProperty dependency)
        {
            if (dependency.HasValues)
            {
                return dependency["version"].Value<string>();
            }
            else
            {
                return dependency.Value<string>();
            }
        }

        private static bool IsV1(JProperty dependency)
        {
            if (dependency.HasValues)
            {
                return dependency["version"].Value<string>().StartsWith("1.0.0");
            }
            else
            {
                return dependency.Value<string>().StartsWith("1.0.0");
            }
        }
    }
}
