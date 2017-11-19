using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LibGit2Sharp;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace Axis.Crux.MSBuildTarget
{
    public class BranchVersioner : Task
    {
        public static readonly Regex ReleaseVersion = new Regex(@"^\d+(\.\d+){2}$");
        public static readonly Regex PreReleaseVersion = new Regex(@"^\d+(\.\d+){2}[^""]+$");
        public static readonly Regex ReleaseVersionTrimmer = new Regex(@"^\d+(\.\d+){2}");
        public static readonly Regex AssemblyInfoPattern = new Regex(@"\[\s*assembly\s*\:\s+AssemblyVersion\s*\(\s*\""\s*\d+(\.\d+){2}[^""]*\""\s*\)\s*\]");
        public static readonly Regex AssemblyFileInfoPattern = new Regex(@"\[\s*assembly\s*\:\s+AssemblyFileVersion\s*\(\s*\""\s*\d+(\.\d+){2}[^""]*\""\s*\)\s*\]");

        private Options Options { get; set; }
        private SemVer ReleaseSemVer { get; set; }
        
        public string ProjectDirectory { get; set; }
        public string ProjectName { get; set; }
        public string OutputPath { get; set; }
        public string AssemblyName { get; set; }
        public string BuildConfiguration { get; set; }


        public override bool Execute()
        {
            Log.LogMessage($@"Project Directory is: {ProjectDirectory}");

            //1. Extract the options file
            AcquireOptions();

            //2. extract the version
            ExtractVersion();

            //3. rewrite this projects AssemblyInfo.cs file
            WriteAssemblyVersion();

            //4. modify nuspec file
            UpdateNuspec();

            return true;
        }

        public void AcquireOptions()
        {
            var optionFile = new FileInfo(Path.Combine(ProjectDirectory, "BranchVersioner.json"));
            if (!optionFile.Exists) Options = new Options { BuildBranch = "origin/master" };
            else
            {
                Options = new StreamReader(optionFile.OpenRead())
                    .Using(_reader => _reader.ReadToEnd())
                    .Pipe(JsonConvert.DeserializeObject<Options>);
            }
        }

        public void ExtractVersion()
        => new Repository(new DirectoryInfo(Path.Combine(ProjectDirectory, "..")).FullName).Using(gitRepo =>
        {
            var solutionDirectory = new DirectoryInfo(Path.Combine(ProjectDirectory, ".."));

            Log.LogMessage($@"Solution directory is: {solutionDirectory}");

            //1. get the last release branch merged into master
            var monitored = gitRepo.Branches[Options.BuildBranch];
            Log.LogMessage($@"Git master branch found: {monitored?.FriendlyName ?? "null"}");

            var releases = gitRepo
                .Branches
                .Remotes()
                .Where(_b => _b.FriendlyName.ToLower().StartsWith("origin/release-"))
                .ToArray();

            var now = DateTime.Now;
            var milliseconds = new TimeSpan(0, now.Hour, now.Minute, now.Second, now.Millisecond).TotalMilliseconds;

            if (releases.Length == 0)
                ReleaseSemVer = new SemVer($"0.0.0-pre-{now.ToString("yyyyMMdd")}-{milliseconds}");

            else
            {
                var recentRelease = monitored
                    .Commits
                    .Select(_c => new
                    {
                        Release = releases.FirstOrDefault(_r => _r.Commits.First().Sha == _c.Sha),
                        Commit = _c
                    })
                    .FirstOrDefault(_rr => _rr.Release != null);

                Log.LogMessage($@"Release branch found: {recentRelease.Release?.FriendlyName ?? "null"}");


                //2. extract the version number from it
                var releaseVersion = recentRelease.Release?.FriendlyName.Substring("origin/release-".Length);

                if (PreReleaseVersion.IsMatch(releaseVersion))
                    releaseVersion = $"{ReleaseVersionTrimmer.Match(releaseVersion).Value}-pre-{now.ToString("yyyyMMdd")}-{milliseconds}";

                ReleaseSemVer = new SemVer(releaseVersion);
            }
        });

        public void WriteAssemblyVersion()
        {
            var assemblyInfoFile = new FileInfo(Path.Combine(ProjectDirectory, "Properties", "AssemblyInfo.cs"));

            var content = new StreamReader(assemblyInfoFile.OpenRead())
                .Using(_reader => _reader.ReadLines()) //<-- disposes the reader
                .Where(_line => !AssemblyFileInfoPattern.IsMatch(_line))
                .Where(_line => !AssemblyInfoPattern.IsMatch(_line))
                .Concat(new[]
                {
                    $@"[assembly: AssemblyVersion(""{ReleaseSemVer.ToString(true)}"")]",
                    $@"[assembly: AssemblyFileVersion(""{ReleaseSemVer.ToString(true)}"")]",
                    Environment.NewLine
                })
                .JoinUsing(Environment.NewLine);
            
            new StreamWriter(new FileStream(assemblyInfoFile.FullName, FileMode.Create)).Using(_writer => _writer.Write(content)); //<-- disposes the writer
        }

        public void UpdateNuspec()
        {
            var nuspecFile = new FileInfo(Path.Combine(ProjectDirectory, $"{ProjectName}.nuspec"));
            if (nuspecFile.Exists)
            {
                var nuspec = nuspecFile
                    .OpenRead()
                    .Using(XDocument.Load);

                //update the Id
                nuspec
                    .Element("package")
                    .Element("metadata")
                    .Element("id")
                    .Value = ProjectName;

                //update the version
                Log.LogMessage($"Nuspec file found! Modifying version...");
                nuspec
                    .Element("package")
                    .Element("metadata")
                    .Element("version")
                    .Value = ReleaseSemVer.ToString();

                //validate dependencies. Note that for now, only package.config projects are supported.
                var packageFile = new FileInfo(Path.Combine(ProjectDirectory, $"packages.config"));
                if (packageFile.Exists)
                {
                    Log.LogMessage($"package.config found! Now verifying dependencies...");
                    var package = packageFile
                        .OpenRead()
                        .Using(XDocument.Load);

                    var nuspecDependeicies = nuspec
                        .Element("package")
                        .Element("metadata")
                        .Element("dependencies");
                    nuspecDependeicies?.RemoveNodes(); //clear it's children

                    if(nuspecDependeicies==null)
                        nuspec.Element("package").Element("metadata").Add(nuspecDependeicies = new XElement("dependencies"));

                    //translate the packages to nuspec dependencies
                    package
                        .Element("packages")
                        .Elements("package")
                        .Select(_n =>
                        {
                            var dependency = new XElement("dependency");
                            dependency.Add(new XAttribute("id", _n.Attribute("id").Value),
                                           new XAttribute("version", _n.Attribute("version").Value));

                            return dependency;
                        })
                        .Pipe(_dependencies => nuspecDependeicies.Add(_dependencies.ToArray()));
                }

                //add the library dll
                var nuspecFiles = nuspec
                    .Element("package")
                    .Element("files");
                nuspecFiles?.RemoveNodes();

                if(nuspecFiles == null)
                    nuspec.Element("package").Add(nuspecFiles = new XElement("files"));

                if(Options.Includes?.Length > 0)
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

                //finally, include the project's dll if default copy is not overridden
                if (!Options.IsDefaultAssemblyCopyOverridden)
                {
                    var dll = new XElement("file");
                    dll.Add(new XAttribute("src", $@"{OutputPath}{AssemblyName}.dll"),
                            new XAttribute("target", "lib"));
                    nuspecFiles.Add(dll);
                }

                Log.LogMessage($"Updating {ProjectName}.nuspec file...");
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
                .Replace($"$({nameof(BuildConfiguration)})", BuildConfiguration)
                .Replace($"$(ReleaseVersion)", ReleaseSemVer.ToString());
        }
    }
}