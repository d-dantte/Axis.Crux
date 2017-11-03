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
        private static readonly Regex ReleaseVersion = new Regex(@"^\d+(\.\d+){2}$");
        private static readonly Regex PreReleaseVersion = new Regex(@"^\d+(\.\d+){2}[^""]+$");
        private static readonly Regex ReleaseVersionTrimmer = new Regex(@"^\d+(\.\d+){2}");
        private static readonly Regex AssemblyInfoPattern = new Regex(@"\[\s*assembly\s*\:\s+AssemblyVersion\s*\(\s*\""\s*\d+(\.\d+){2}[^""]*\""\s*\)\s*\]");
        private static readonly Regex AssemblyFileInfoPattern = new Regex(@"\[\s*assembly\s*\:\s+AssemblyFileVersion\s*\(\s*\""\s*\d+(\.\d+){2}[^""]*\""\s*\)\s*\]");


        public string MSBuildProjectDirectory { get; set; }

        public override bool Execute()
        {
            //1. extract the version
            var releaseVersion = ExtractVersion();

            //2. rewrite this projects AssemblyInfo.cs file
            WriteVersion(releaseVersion);

            return true;
        }

        public string ExtractVersion()
        => new Repository(new DirectoryInfo(Path.Combine(MSBuildProjectDirectory, "..")).FullName).Using(gitRepo =>
        {
            var solutionDirectory = new DirectoryInfo(Path.Combine(MSBuildProjectDirectory, ".."));

            //1. get the last release branch merged into master
            var main = gitRepo.Branches["master"];
            var releases = gitRepo
                .Branches
                .Remotes()
                .Where(_b => _b.FriendlyName.ToLower().StartsWith("origin/release-"))
                .ToArray();

            var now = DateTime.Now;
            var milliseconds = new TimeSpan(0, now.Hour, now.Minute, now.Second, now.Millisecond).TotalMilliseconds;

            if (releases.Length == 0)
                return $"0.0.0-{now.ToString("yyyyMMdd")}-{milliseconds}";

            var recentRelease = main
                .Commits
                .Select(_c => new
                {
                    Release = releases.FirstOrDefault(_r => _r.Commits.First().Sha == _c.Sha),
                    Commit = _c
                })
                .FirstOrDefault(_rr => _rr.Release != null);

            var releaseVersion = recentRelease.Release.FriendlyName.Substring("origin/release-".Length);
            
            //2. extract the version number from it
            if (PreReleaseVersion.IsMatch(releaseVersion))
                releaseVersion = $"{ReleaseVersionTrimmer.Match(releaseVersion).Value}-{now.ToString("yyyyMMdd")}-{milliseconds}";

            return releaseVersion;
        });

        public void WriteVersion(string releaseVersion)
        {
            var assemblyInfoFile = new FileInfo(Path.Combine(MSBuildProjectDirectory, "Properties", "AssemblyInfo.cs"));

            var content = new StreamReader(assemblyInfoFile.OpenRead())
                .Using(_reader => _reader.ReadLines()) //<-- disposes the reader
                .Where(_line => !AssemblyFileInfoPattern.IsMatch(_line))
                .Where(_line => !AssemblyInfoPattern.IsMatch(_line))
                .Concat(new[]
                {
                    $@"[assembly: AssemblyVersion(""{releaseVersion}"")]",
                    $@"[assembly: AssemblyFileVersion(""{releaseVersion}"")]"
                })
                .JoinUsing("\n");
            
            new StreamWriter(new FileStream(assemblyInfoFile.FullName, FileMode.Create)).Using(_writer => _writer.Write(content)); //<-- disposes the writer
        }
    }
}
