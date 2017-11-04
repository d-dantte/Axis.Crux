using System;

namespace Axis.Crux.MSBuildTarget
{
    public class SemVer
    {
        public uint Major { get; set; }
        public uint Minor { get; set; }
        public uint Patch { get; set; }
        public string Pre { get; set; }

        public override string ToString() => ToString(false);

        public string ToString(bool excludePre)
        {
            var version = $@"{Major}.{Minor}.{Patch}";
            if (!excludePre && !string.IsNullOrWhiteSpace(Pre)) version += $"{Pre}";

            return version;
        }

        public SemVer()
        { }

        public SemVer(string semver)
        {
            if (BranchVersioner.ReleaseVersion.IsMatch(semver))
            {
                var parts = semver.Split('.');
                Major = uint.Parse(parts[0]);
                Minor = uint.Parse(parts[1]);
                Patch = uint.Parse(parts[2]);
            }
            else if (BranchVersioner.PreReleaseVersion.IsMatch(semver))
            {
                var parts = semver.Split('.', '-');
                Major = uint.Parse(parts[0]);
                Minor = uint.Parse(parts[1]);
                Patch = uint.Parse(parts[2]);
                Pre = semver.Substring(parts[0].Length + parts[1].Length + parts[2].Length + 2);
            }
            else throw new Exception("invalid SemVer format");
        }
    }
}
