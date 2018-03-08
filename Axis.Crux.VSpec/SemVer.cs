using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Axis.Crux.VSpec
{
    public class SemVer
    {
        public static readonly Regex ReleaseVersion = new Regex(@"^\d+(\.\d+){2}$");
        public static readonly Regex PreReleaseIndicator = new Regex(@"^\d+(\.\d+){2}-pre(release)?$", RegexOptions.IgnoreCase);
        public static readonly Regex PreReleaseVersionPrefix = new Regex(@"^\d+(\.\d+){2}-pre(release)?[-\.]", RegexOptions.IgnoreCase);
        public static readonly Regex PreReleaseVersion = new Regex(@"^\d+(\.\d+){2}-pre(release)?[-\.]\d[-\.\d]+$", RegexOptions.IgnoreCase);

        public static readonly SemVer Genesis = new SemVer("0.0.0");
        public static readonly SemVer PreGenesis = new SemVer("0.0.0-pre");

        public SemVer()
        { }

        public SemVer(string semver)
        {
            if (ReleaseVersion.IsMatch(semver))
            {
                var parts = semver.Split('.');
                Major = uint.Parse(parts[0]);
                Minor = uint.Parse(parts[1]);
                Patch = uint.Parse(parts[2]);
            }
            else if (PreReleaseIndicator.IsMatch(semver))
            {
                var parts = semver.Split('.', '-');

                var now = DateTime.Now;
                var milliseconds = new TimeSpan(0, now.Hour, now.Minute, now.Second, now.Millisecond).TotalMilliseconds;

                Major = uint.Parse(parts[0]);
                Minor = uint.Parse(parts[1]);
                Patch = uint.Parse(parts[2]);
                Pre = $"{now.ToString("yyyyMMdd")}-{Pad(12, milliseconds)}";
            }
            else if (PreReleaseVersion.IsMatch(semver))
            {
                var parts = semver.Split('.', '-');

                Major = uint.Parse(parts[0]);
                Minor = uint.Parse(parts[1]);
                Patch = uint.Parse(parts[2]);
                Pre = semver.Replace(PreReleaseVersionPrefix.Match(semver).Value, "");
            }
            else throw new Exception("invalid SemVer format");
        }

        
        public uint Major { get; set; }
        public uint Minor { get; set; }
        public uint Patch { get; set; }
        public string Pre { get; set; }


        public override string ToString() => ToString(false);

        public string ToString(bool excludePre)
        {
            var version = $@"{Major}.{Minor}.{Patch}";
            if (!excludePre && !string.IsNullOrWhiteSpace(Pre)) version += $"-pre-{Pre}";

            return version;
        }

        public static SemVer Parse(string semver) => new SemVer(semver);

        private static string Pad(int digitPlaces, double value)
        {
            var svalue = value.ToString();
            var diff = digitPlaces - svalue.Length;
            if (diff < 0) return svalue;
            else return $"{Zeros(diff)}{svalue}";
        }

        private static string Zeros(int count)
        {
            var sbuff = new StringBuilder();
            for (int cnt = 0; cnt < count; cnt++)
                sbuff.Append("0");

            return sbuff.ToString();
        }
    }
}
