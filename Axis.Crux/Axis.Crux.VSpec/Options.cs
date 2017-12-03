using System.Linq;

namespace Axis.Crux.VSpec
{
    public class Options
    {
        public PackageVersion[] Versions { get; set; }
        public FileInclusion[] Includes { get; set; }

        public bool? IsAutoAssemblyCopyEnabled { get; set; } = true;

        public PackageVersion MostRecentVersion() => Versions?.LastOrDefault() ?? new PackageVersion { Version = SemVer.PreGenesis };
    }

    public class PackageVersion
    {
        public SemVer Version { get; set; }
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
