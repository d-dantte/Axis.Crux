using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace Axis.Crux.VSpec
{
    public class PackageVersioner : Task
    {
        public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new SemVerConverter()
            }
        };

        public static readonly Regex CsProjPattern = new Regex("\\.csproj$");


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
                    new PackageVersion
                    {
                        Version = new SemVerRange(SemVer.PreGenesis.ToString())
                    }
                }
            };
            else
            {
                Options = new StreamReader(optionFile.OpenRead())
                    .Using(reader => reader.ReadToEnd())
                    .Pipe(value => JsonConvert.DeserializeObject<Options>(value, JsonSerializerSettings));
            }
        }

        public void UpdateNuspec()
        {
            var nuspecFile = new FileInfo(Path.Combine(ProjectDirectory, $"{ProjectName}.nuspec"));

            if (!nuspecFile.Exists) return;

            var nuspec = nuspecFile
                .OpenRead()
                .Using(XDocument.Load);
            Log.LogMessage($"Nuspec file found!");

            #region Nuspec ID

            //update the Id
            Log.LogMessage($"Updating nuspec Id...");
            nuspec
                .Element("package")
                .Element("metadata")
                .Element("id")
                .Value = ProjectName;

            #endregion

            #region Nuspec Version

            //update the version
            Log.LogMessage($"Updating version...");
            var packageVersion = Options.MostRecentVersion();
            nuspec
                .Element("package")
                .Element("metadata")
                .Element("version")
                .Value = packageVersion.Version.ToString();

            #endregion

            #region Nuspec Release Notes

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

            #endregion

            #region Nuspec Dependencies

            var nuspecDependencies = nuspec
                .Element("package")
                .Element("metadata")
                .Element("dependencies");
            nuspecDependencies?.RemoveNodes();

            if (nuspecDependencies == null) nuspec
                .Element("package")
                .Element("metadata")
                .Add(nuspecDependencies = new XElement("dependencies"));

            //add dependencies from ProjectDirectory/package.config
            var packageFile = new FileInfo(Path.Combine(ProjectDirectory, "packages.config"));
            if (packageFile.Exists && Options.IsAutoDependencyCopyEnabled != false)
            { 
                var packageConfig = packageFile
                    .OpenRead()
                    .Using(XDocument.Load);

                packageConfig
                    .Element("packages")
                    .Elements("package")
                    .Where(_elt => !bool.Parse(_elt.Attribute("developmentDependency")?.Value ?? "false"))
                    .Select(_elt => new XElement(
                        "dependency",
                        new XAttribute("id", _elt.Attribute("id").Value),
                        new XAttribute("version", _elt.Attribute("version").Value)
                    ))
                    .Pipe(nuspecDependencies.Add);
            }

            //add dependencies form .csproj project dependencies that have nuspec files
            var csprojFile = new FileInfo(Path.Combine(ProjectDirectory, $"{ProjectName}.csproj"));
            if (csprojFile.Exists)
            {
                var xdoc = csprojFile
                    .OpenRead()
                    .Using(XDocument.Load);
                
                if (Options.IsCsprojProjectDependencyLookupEnabled == true)
                {
                    var @namespace = xdoc.Root.Name.Namespace;

                    //get all project-references... 
                    xdoc.Root
                        .Elements(@namespace + "ItemGroup")
                        .SelectMany(_itemGroup => _itemGroup.Elements())
                        .Where(IsProjectReference)
                        .Where(ProjectRefHasNuspec) //...that have nuspec files defined in them.
                        .Select(ExtractNuspecIdentityFromProjectRef) //extract the nuspec identity (nuspec id & version) as a nuspec dependency node
                        .Where(IsNewDependency(nuspecDependencies)) //then filter out existing dependencies
                        .Pipe(nuspecDependencies.Add); //add "new" dependency nodes to our nuspec dependencies
                }
                else if (Options.IsCsprojPackageDependencyLookupEnabled == true)
                {
                    var @namespace = xdoc.Root.Name.Namespace;

                    //get all package-references... 
                    xdoc.Root
                        .Elements(@namespace + "ItemGroup")
                        .SelectMany(_itemGroup => _itemGroup.Elements())
                        .Where(IsPackageReference)
                        .Select(TranslateToNuspecDependency) //extract the nuspec identity (nuspec id & version) as a nuspec dependency node
                        .Where(IsNewDependency(nuspecDependencies)) //then filter out existing dependencies
                        .Pipe(nuspecDependencies.Add); //add "new" dependency nodes to our nuspec dependencies
                }

                else throw new Exception("Invalid .csproj");
            }

            #endregion

            #region Nuspec File includes

            Log.LogMessage($"Updating included files...");
            var nuspecFiles = nuspec
                .Element("package")
                .Element("files");
            nuspecFiles?.RemoveNodes();

            if (nuspecFiles == null)
                nuspec.Element("package").Add(nuspecFiles = new XElement("files"));

            //add the library dll
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
            if (Options.IsAutoAssemblyCopyEnabled != false)
            {
                var dll = new XElement("file");
                dll.Add(new XAttribute("src", $@"{OutputPath}{AssemblyName}.dll"),
                    new XAttribute("target", "lib"));
                nuspecFiles.Add(dll);
            }

            #endregion

            Log.LogMessage($"Exporting {ProjectName}.nuspec file...");
            nuspec.Save(nuspecFile.FullName);
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

        private static bool IsProjectReference(XElement element)
        {
            return element.Name == element.Name.Namespace + "ProjectReference";
        }
        private static bool IsPackageReference(XElement element)
        {
            return element.Name == element.Name.Namespace + "PackageReference";
        }
        private static bool ProjectRefHasNuspec(XElement projectReference)
        {
            var finfo = new FileInfo(CsProjPattern.Replace(projectReference.Attribute("Include").Value, ".nuspec"));            
            return finfo.Exists;
        }
        private static XElement ExtractNuspecIdentityFromProjectRef(XElement projectReference)
        {
            var nuspec = new FileInfo(CsProjPattern.Replace(projectReference.Attribute("Include").Value, ".nuspec"))
                .OpenRead()
                .Using(XDocument.Load);

            var metadata = nuspec
                .Element("package")
                .Element("metadata");
            
            return new XElement(
                "dependency",
                new XAttribute("id", metadata.Element("id").Value),
                new XAttribute("version", metadata.Element("version").Value)
            );
        }

        private static XElement TranslateToNuspecDependency(XElement packageReference)
        {
            return new XElement(
                "dependency",
                new XAttribute("id", packageReference.Attribute("Include").Value),
                new XAttribute("version", packageReference.Attribute("Version").Value));
        }
        private static Func<XElement, bool> IsNewDependency(XElement nuspecDependencies)
        {
            return (dependency) => nuspecDependencies
                .Elements("dependency")
                .All(nuspecDep => nuspecDep.Attribute("id").Value != dependency.Attribute("id").Value);
        }


        private static readonly string _LogFileName = $"Log-{DateTime.Now.Ticks}.txt";
        private void __log(object value)
        {
            new FileStream(_LogFileName, FileMode.Append)
                .Using(_str =>
                {
                    var writer = new StreamWriter(_str);
                    writer.WriteLine(value?.ToString() ?? "null");
                    writer.Flush();
                    _str.Flush();
                });
        }
    }
}