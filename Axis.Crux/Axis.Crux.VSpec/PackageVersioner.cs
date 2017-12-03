using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace Axis.Crux.VSpec
{
    public class PackageVersioner : Task
    {
        public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new SemVerConverter()
            }
        };

        private Options Options { get; set; }

        public string ProjectDirectory { get; set; }
        public string ProjectName { get; set; }
        public string OutputPath { get; set; }
        public string AssemblyName { get; set; }
        public string BuildConfiguration { get; set; }


        public override bool Execute()
        {
            //1. Acquire options
            AcquireOptions();
            Log.LogMessage($@"VSpec.json imported");

            //2. update Nuspec file
            UpdateNuspec();
            Log.LogMessage($@"{ProjectName}.nuspec updated");

            return true;
        }

        public void AcquireOptions()
        {
            var optionFile = new FileInfo(Path.Combine(ProjectDirectory, "VSpec.json"));
            if (!optionFile.Exists) Options = new Options
            {
                Versions = new[]
                {
                    new PackageVersion{ Version = SemVer.PreGenesis }
                }
            };
            else
            {
                Options = new StreamReader(optionFile.OpenRead())
                    .Using(_reader => _reader.ReadToEnd())
                    .Pipe(_v => JsonConvert.DeserializeObject<Options>(_v, JsonSerializerSettings));
            }
        }

        public void UpdateNuspec()
        {
            var nuspecFile = new FileInfo(Path.Combine(ProjectDirectory, $"{ProjectName}.nuspec"));
            if (nuspecFile.Exists)
            {
                var nuspec = nuspecFile
                    .OpenRead()
                    .Using(XDocument.Load);
                Log.LogMessage($"Nuspec file found!");

                //update the Id
                Log.LogMessage($"Updating nuspec Id...");
                nuspec
                    .Element("package")
                    .Element("metadata")
                    .Element("id")
                    .Value = ProjectName;


                //update the version
                Log.LogMessage($"Updating version...");
                var packageVersion = Options.MostRecentVersion();
                nuspec
                    .Element("package")
                    .Element("metadata")
                    .Element("version")
                    .Value = packageVersion.Version.ToString();


                //update release notes if available
                if (!string.IsNullOrWhiteSpace(packageVersion.ReleaseNotes))
                {
                    Log.LogMessage($"Updating release notes...");
                    nuspec
                        .Element("package")
                        .Element("metadata")
                        .Element("releaseNotes")
                        .Value = packageVersion.ReleaseNotes;
                }


                //add the library dll
                Log.LogMessage($"Updating included files...");
                var nuspecFiles = nuspec
                    .Element("package")
                    .Element("files");
                nuspecFiles?.RemoveNodes();

                if (nuspecFiles == null)
                    nuspec.Element("package").Add(nuspecFiles = new XElement("files"));

                if (Options.Includes?.Length > 0)
                {
                    Options.Includes
                        .Select(_include =>
                        {
                            var file = new XElement("file");
                            file.Add(new XAttribute("src", ResolveParams(_include.Source)),
                                     new XAttribute("target", ResolveParams(_include.TargetPath)));
                            if (!string.IsNullOrWhiteSpace(_include.Exclude))
                                file.Add(new XAttribute("exclude", ResolveParams(_include.Exclude)));

                            return file;
                        })
                        .Pipe(_files => nuspecFiles.Add(_files.ToArray()));
                }


                //finally, include the project's dll if auto-copy is not enabled
                if (Options.IsAutoAssemblyCopyDisabled)
                {
                    var dll = new XElement("file");
                    dll.Add(new XAttribute("src", $@"{OutputPath}{AssemblyName}.dll"),
                            new XAttribute("target", "lib"));
                    nuspecFiles.Add(dll);
                }

                Log.LogMessage($"Exporting {ProjectName}.nuspec file...");
                nuspec.Save(nuspecFile.FullName);
            }
        }

        public string ResolveParams(string value)
        {
            return value?
                .Replace($"$({nameof(ProjectDirectory)})", ProjectDirectory)
                .Replace($"$({nameof(ProjectName)})", ProjectName)
                .Replace($"$({nameof(OutputPath)})", OutputPath)
                .Replace($"$({nameof(AssemblyName)})", AssemblyName)
                .Replace($"$({nameof(BuildConfiguration)})", BuildConfiguration);
        }
    }
}
