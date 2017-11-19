namespace Axis.Crux.MSBuildTarget
{
    public class Options
    {
        public string BuildBranch { get; set; }
        public FileInclusion[] Includes { get; set; }
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
