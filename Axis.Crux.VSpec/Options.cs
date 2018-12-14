using System.Linq;

namespace Axis.Crux.VSpec
{
    public class Options
    {
        public PackageVersion[] Versions { get; set; }
        public FileInclusion[] Includes { get; set; }

        /// <summary>
        /// Determines if this projects dll file should be rewritten in the nuspec
        /// file every time the package versioner task runs.
        /// </summary>
        public bool? IsAutoAssemblyCopyEnabled { get; set; } = true;

        /// <summary>
        /// Determines if dependencies should be written by the package versioner task into the nuspec
        /// file. With this flag, Dependencies are gathered from package.config files (.net Framework projects)
        /// </summary>
        public bool? IsAutoDependencyCopyEnabled { get; set; } = true;

        /// <summary>
        /// Determines if dependencies should be written by the package versioner task into the nuspec
        /// file. With this flag, Dependencies are gathered from the "PackageReference" in the.csproj files
        /// (.net standard/core projects)
        /// </summary>
        public bool? IsCsprojPackageDependencyLookupEnabled { get; set; } = false;

        /// <summary>
        /// Determines if dependencies should be written by the package versioner task into the nuspec
        /// file. With this flag, Dependencies are gathered from the "project reference" int the .csproj files
        /// (.net Framework/standard/core projects)
        /// </summary>
        public bool? IsCsprojProjectDependencyLookupEnabled { get; set; } = false;

        public PackageVersion MostRecentVersion() => Versions?.LastOrDefault() ?? new PackageVersion
        {
            Version = new SemVerRange(SemVer.PreGenesis.ToString())
        };
    }

    public class PackageVersion
    {
        public SemVerRange Version { get; set; }
        public string ReleaseNotes { get; set; }

        //other version specific stuff can come here
    }

    public class FileInclusion
    {
        public string Source { get; set; }

        /// <summary>
        /// Relative to the nupkg root
        /// </summary>
        public string TargetPath { get; set; }

        public string Exclude { get; set; }
    }
}
