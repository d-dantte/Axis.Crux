using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using Microsoft.Build.Utilities;

namespace Axis.Crux.MSBuildTarget
{
    public class BranchVersioner : Task
    {
        public static readonly Regex ReleaseVersion = new Regex(@"^\d+(\.\d+){2}$");
        public static readonly Regex PreReleaseVersion = new Regex(@"^\d+(\.\d+){2}[^""]+$");
        public static readonly Regex ReleaseVersionTrimmer = new Regex(@"^\d+(\.\d+){2}");
        public static readonly Regex AssemblyInfoPattern = new Regex(@"\[\s*assembly\s*\:\s+AssemblyVersion\s*\(\s*\""\s*\d+(\.\d+){2}[^""]*\""\s*\)\s*\]");
        public static readonly Regex AssemblyFileInfoPattern = new Regex(@"\[\s*assembly\s*\:\s+AssemblyFileVersion\s*\(\s*\""\s*\d+(\.\d+){2}[^""]*\""\s*\)\s*\]");

        public static readonly string PackageVersionVariable = "CI_Version";


        public string MSBuildProjectDirectory { get; set; }

        public override bool Execute()
        {
            this.Log.LogMessage($@"Project Directory is: {MSBuildProjectDirectory}");

            //1. extract the version
            var releaseVersion = ExtractVersion();

            //2. rewrite this projects AssemblyInfo.cs file
            WriteVersion(releaseVersion);

            //3. Set the environment variable to be picked up by the nuget packager
            SetEnvironmentVariable(releaseVersion);

            return true;
        }

        public SemVer ExtractVersion()
        => new Repository(new DirectoryInfo(Path.Combine(MSBuildProjectDirectory, "..")).FullName).Using(gitRepo =>
        {
            var solutionDirectory = new DirectoryInfo(Path.Combine(MSBuildProjectDirectory, ".."));

            Log.LogMessage($@"Solution directory is: {solutionDirectory}");

            //1. get the last release branch merged into master
            var main = gitRepo.Branches["origin/master"];
            Log.LogMessage($@"Git master branch found: {main?.FriendlyName ?? "null"}");

            var releases = gitRepo
                .Branches
                .Remotes()
                .Where(_b => _b.FriendlyName.ToLower().StartsWith("origin/release-"))
                .ToArray();

            var now = DateTime.Now;
            var milliseconds = new TimeSpan(0, now.Hour, now.Minute, now.Second, now.Millisecond).TotalMilliseconds;

            if (releases.Length == 0)
                return new SemVer($"0.0.0-{now.ToString("yyyyMMdd")}-{milliseconds}");

            var recentRelease = main
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
                releaseVersion = $"{ReleaseVersionTrimmer.Match(releaseVersion).Value}-{now.ToString("yyyyMMdd")}-{milliseconds}";

            return new SemVer(releaseVersion);
        });

        public void WriteVersion(SemVer releaseVersion)
        {
            var assemblyInfoFile = new FileInfo(Path.Combine(MSBuildProjectDirectory, "Properties", "AssemblyInfo.cs"));

            var content = new StreamReader(assemblyInfoFile.OpenRead())
                .Using(_reader => _reader.ReadLines()) //<-- disposes the reader
                .Where(_line => !AssemblyFileInfoPattern.IsMatch(_line))
                .Where(_line => !AssemblyInfoPattern.IsMatch(_line))
                .Concat(new[]
                {
                    $@"[assembly: AssemblyVersion(""{releaseVersion.ToString(true)}"")]",
                    $@"[assembly: AssemblyFileVersion(""{releaseVersion.ToString(false)}"")]"
                })
                .JoinUsing("\n");
            
            new StreamWriter(new FileStream(assemblyInfoFile.FullName, FileMode.Create)).Using(_writer => _writer.Write(content)); //<-- disposes the writer
        }

        public void SetEnvironmentVariable(SemVer releaseVersion)
        {
            Log.LogMessage($"Setting Package version to Environment variable: {PackageVersionVariable}");

            Environment.SetEnvironmentVariable(PackageVersionVariable, releaseVersion.ToString(false));
        }
    }
}

