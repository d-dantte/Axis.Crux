using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Axis.Crux.VSpec
{
    public class SemVer
    {
        public static readonly Regex ReleaseVersion = new Regex(@"^\d+(\.\d+){2}$");
        public static readonly Regex ReleaseVersionMatcher = new Regex(@"^\d+(\.\d+){2}");
        public static readonly Regex PreReleaseLabeled = new Regex(@"^\d+(\.\d+){2}-[\w]+$", RegexOptions.IgnoreCase);
        public static readonly Regex PreReleaseWildcardLabeled = new Regex(@"^\d+(\.\d+){2}-[\w]+-\*$", RegexOptions.IgnoreCase);
        public static readonly Regex PreReleaseVersion = new Regex(@"^\d+(\.\d+){2}-[\w-]+(\.[\w-]+)*$", RegexOptions.IgnoreCase);

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
            else if (PreReleaseLabeled.IsMatch(semver))
            {
                var parts = semver.Split('.', '-');

                var now = DateTime.Now;
                var milliseconds = new TimeSpan(0, now.Hour, now.Minute, now.Second, now.Millisecond).TotalMilliseconds;

                Major = uint.Parse(parts[0]);
                Minor = uint.Parse(parts[1]);
                Patch = uint.Parse(parts[2]);
                Pre = parts[3];
            }
            else if (PreReleaseWildcardLabeled.IsMatch(semver))
            {
                var parts = semver.Split('.', '-');

                var now = DateTime.Now;
                var milliseconds = new TimeSpan(0, now.Hour, now.Minute, now.Second, now.Millisecond).TotalMilliseconds;

                Major = uint.Parse(parts[0]);
                Minor = uint.Parse(parts[1]);
                Patch = uint.Parse(parts[2]);
                Pre = $"{parts[3]}-{now:yyyyMMdd}-{Pad(12, milliseconds)}";
            }
            else if (PreReleaseVersion.IsMatch(semver))
            {
                var parts = semver.Split('.', '-');

                Major = uint.Parse(parts[0]);
                Minor = uint.Parse(parts[1]);
                Patch = uint.Parse(parts[2]);
                Pre = semver
                    .Replace(ReleaseVersionMatcher.Match(semver).Value, "")
                    .TrimStart('-');
            }
            else throw new Exception($"Invalid SemVer Format: {semver}");
        }

        
        public uint Major { get; set; }
        public uint Minor { get; set; }
        public uint Patch { get; set; }
        public string Pre { get; set; }


        public override string ToString() => ToString(false);

        public string ToString(bool excludePre)
        {
            var version = $@"{Major}.{Minor}.{Patch}";
            if (!excludePre && !string.IsNullOrWhiteSpace(Pre)) version += $"-{Pre}";

            return version;
        }

        public static SemVer Parse(string semver) => new SemVer(semver);

        private static string Pad(int digitPlaces, double value)
        {
            var stringValue = value.ToString(CultureInfo.InvariantCulture);
            var diff = digitPlaces - stringValue.Length;
            return diff < 0 ? stringValue : $"{Zeros(diff)}{stringValue}";
        }

        private static string Zeros(int count)
        {
            var stringBuilder = new StringBuilder();
            for (var cnt = 0; cnt < count; cnt++)
                stringBuilder.Append("0");

            return stringBuilder.ToString();
        }
    }

    public enum SemVerBoundaryType
    {
        Inclusive,
        Exclusive
    }

    public class Boundary
    {
        public SemVer Version { get; set; }
        public SemVerBoundaryType BoundaryType { get; set; }
    }

    public class SemVerRange
    {
        public static readonly Regex PreReleaseVersion = 
            new Regex(@"^[\(\[]?\d+(\.\d+){2}-[\w-]+(\.[\w-]+)(,)?[\)\]]$", RegexOptions.IgnoreCase);

        public Boundary LowerBound { get; set; }
        public Boundary UpperBound { get; set; }

        public SemVerRange(string semver)
        {
            if (string.IsNullOrWhiteSpace(semver))
                throw new Exception($"Invalid SemVer Range {semver}");

            if (!((HasValidRangeLowerBound(semver) && HasValidRangeUpperBound(semver))
                  || (!HasValidRangeUpperBound(semver) && !HasValidRangeLowerBound(semver))))
                throw new Exception($"Invalid SemVer Range {semver}");

            var parts = semver.Split(',');

            switch (parts.Length)
            {
                case 1:
                {
                    if (HasExclusiveUpperBound(parts[0]) || HasExclusiveLowerBound(parts[0]))
                        throw new Exception($"Invalid SemVer Range {semver}");

                    var lowerBoundary = new Boundary
                    {
                        BoundaryType = SemVerBoundaryType.Inclusive,
                        Version = SemVer.Parse(parts[0].Trim('[', ']'))
                    };

                    var upperBoundary = new Boundary
                    {
                        BoundaryType = !HasValidRangeUpperBound(parts[0])
                            ? SemVerBoundaryType.Exclusive
                            : SemVerBoundaryType.Inclusive,
                        Version = lowerBoundary.Version
                    };

                    LowerBound = lowerBoundary;
                    UpperBound = upperBoundary;
                    return;
                }

                case 2:
                {
                    var rawVersionLower = parts[0].TrimStart('[', '(');
                    var rawVersionUpper = parts[1].TrimEnd(']', ')');

                    LowerBound = new Boundary
                    {
                        BoundaryType = HasExclusiveLowerBound(parts[0])
                            ? SemVerBoundaryType.Exclusive
                            : SemVerBoundaryType.Inclusive,
                        Version = string.IsNullOrWhiteSpace(rawVersionLower) ? null : new SemVer(rawVersionLower)
                    };
                    UpperBound = new Boundary
                    {
                        BoundaryType = HasExclusiveUpperBound(parts[1])
                            ? SemVerBoundaryType.Exclusive
                            : SemVerBoundaryType.Inclusive,
                        Version = string.IsNullOrWhiteSpace(rawVersionUpper) ? null : new SemVer(rawVersionUpper)
                    };
                    return;
                }

                default:
                    throw new Exception($"Invalid SemVer Range {semver}");
            }
        }

        public static SemVerRange Parse(string semver)
        {
            return new SemVerRange(semver);
        }

        public override string ToString()
        {
            var value =  $"{LowerBoundarySymbol(LowerBound.BoundaryType)}{LowerBound.Version}";
            if (!string.Equals(
                UpperBound.Version?.ToString(), 
                LowerBound.Version?.ToString(),
                StringComparison.InvariantCulture))
            {
                value = $"{value},{UpperBound.Version}{UpperBoundarySymbol(UpperBound.BoundaryType)}";
            }
            else if(LowerBound.BoundaryType == SemVerBoundaryType.Inclusive)
            {
                value = UpperBound.BoundaryType == SemVerBoundaryType.Exclusive ? 
                    value.TrimStart('[') : 
                    $"{value}{UpperBoundarySymbol(SemVerBoundaryType.Inclusive)}";
            }
            else
            {
                value = $"{value},{UpperBound.Version}{UpperBoundarySymbol(UpperBound.BoundaryType)}";
            }

            return value;
        }

        public static bool HasExclusiveLowerBound(string @string) => @string.StartsWith("(");
        public static bool HasInclusiveLowerBound(string @string) => @string.StartsWith("[");

        public static bool HasExclusiveUpperBound(string @string) => @string.EndsWith(")");
        public static bool HasInclusiveUpperBound(string @string) => @string.EndsWith("]");

        public static bool HasValidRangeLowerBound(string @string)
        => HasExclusiveLowerBound(@string) || HasInclusiveLowerBound(@string);
        public static bool HasValidRangeUpperBound(string @string)
        => HasExclusiveUpperBound(@string) || HasInclusiveUpperBound(@string);

        public static char LowerBoundarySymbol(SemVerBoundaryType type)
        => type == SemVerBoundaryType.Inclusive ? '[' :
           type == SemVerBoundaryType.Exclusive ? '(' :
           throw new Exception($"Invalid Boundary Type: {type}");

        public static char UpperBoundarySymbol(SemVerBoundaryType type)
        => type == SemVerBoundaryType.Inclusive ? ']' :
           type == SemVerBoundaryType.Exclusive ? ')' :
           throw new Exception($"Invalid Boundary Type: {type}");
    }
}
