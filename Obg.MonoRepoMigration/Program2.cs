using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Obg.MonoRepoMigration
{
    class Program2
    {
        public static readonly string ArtifactDirectory = "C:\\Betsson\\Tasks\\MonoRepoMigrationArtifacts";

        public static void Main(string[] args)
        {
            //starting from a solution folder...
            Console.Write("Enter Vertical Directory: ");

            var path = Console.ReadLine();
            var directory = new DirectoryInfo(path);
            var artifactRoot = new DirectoryInfo(ArtifactDirectory);
            if (!artifactRoot.Exists)
                artifactRoot.Create();

            var prjTable = new Dictionary<string, Project>();

            if (!directory.Exists)
                Console.WriteLine("Invalid directory specified\nExiting...");

            else
            {
                var projects = directory
                    .EnumerateDirectories()
                    .Select(sub => sub.EnumerateFiles("*.csproj").FirstOrDefault())
                    .Where(Ext.IsNotNull)
                    .Select(finfo => new Project(finfo, new DependencyContext(prjTable)))
                    .ToArray();


                //proceed to rewrite csproj files for old formats
                var xmlSettings = new XmlWriterSettings
                {
                    Indent = true,
                    OmitXmlDeclaration = true,
                    CloseOutput = true,
                    NewLineChars = Environment.NewLine,
                    NewLineHandling = NewLineHandling.Entitize
                };

                projects
                    .Where(IsOldFormat)
                    .ForAll(project =>
                    {
                        //create the directory used to hold all the deleted stuff
                        var artifactDirectory = artifactRoot.CreateSubdirectory(Path.Combine(
                            project.Info.AssemblyName,
                            DateTimeOffset.Now.Ticks.ToString()));

                        //create the new csproj file
                        var csprojDoc = CreateNewCsprojDoc(project);

                        //remove the old csproj
                        MoveFile(project.CsprojFile, artifactDirectory);

                        var sb = new StringBuilder();
                        XmlWriter.Create(
                            sb,
                            xmlSettings)
                            .Using(writer =>
                            {
                                csprojDoc.WriteTo(writer);
                                writer.Flush();
                            });

                        //replace <NL/> with actual new lines
                        sb = sb.Replace("<NL />", "");

                        //write to the underlying file
                        new StreamWriter(project.CsprojFile.OpenWrite())
                            .Using(writer =>
                            {
                                writer.Write(sb.ToString());
                                writer.Flush();
                            });

                        //remove assembly info
                        if (ShouldDeleteAssemblyInfo(project.CsprojFile.Directory, out var assemblyInfoFile))
                        {
                            if (assemblyInfoFile.Exists)
                                MoveFile(assemblyInfoFile, artifactDirectory);

                            if (assemblyInfoFile.Directory.Exists)
                                assemblyInfoFile.Directory.Delete(true);
                        }

                        //remove changelog
                        var changeLogFile = project.CsprojFile.Directory
                            .GetFiles()
                            .FirstOrDefault(file => file.Name.ToLower() == "changelog.md");

                        if (changeLogFile != null)
                            MoveFile(changeLogFile, artifactDirectory);

                        //remove readme file
                        var readmeFile = project.CsprojFile.Directory
                            .GetFiles()
                            .FirstOrDefault(file => file.Name.ToLower() == "readme.md");

                        if (readmeFile != null)
                            MoveFile(readmeFile, artifactDirectory);

                        //remove app.config file
                        var appconfigFile = project.CsprojFile.Directory
                            .GetFiles()
                            .FirstOrDefault(file => file.Name.ToLower() == "app.config");

                        if (appconfigFile != null)
                            MoveFile(appconfigFile, artifactDirectory);

                        //remove package.config file
                        var packageconfigFile = project.CsprojFile.Directory
                            .GetFiles()
                            .FirstOrDefault(file => file.Name.ToLower() == "packages.config");

                        if (packageconfigFile != null)
                            MoveFile(packageconfigFile, artifactDirectory);

                        //remove nuspec.config file
                        var nuspecFile = project.CsprojFile.Directory
                            .GetFiles()
                            .FirstOrDefault(file => file.Extension.ToLower() == ".nuspec");

                        if (nuspecFile != null)
                            MoveFile(nuspecFile, artifactDirectory);

                        //remove obj folder
                        var obg = project.CsprojFile.Directory
                            .EnumerateDirectories("obj")
                            .FirstOrDefault();

                        if (obg?.Exists == true)
                            MoveDirectory(obg, artifactDirectory);
                    });

                //add the new changelog.md file
                CreateReadme(new FileInfo(Path.Combine(directory.FullName, "CHANGELOG.md")));

                //add the new readme.md file
                CreateReadme(new FileInfo(Path.Combine(directory.FullName, "README.md")));

                //add the yaml file
                CreateYaml(new FileInfo(
                    Path.Combine(directory.FullName, "obg-build.yaml")), 
                    directory.ResolveVerticalName(), 
                    projects);
            }

            Console.Write("Done! Press any key to exit...");
            Console.ReadKey();
        }

        private static bool IsOldFormat(Project project) => project.IsNewFormat == false;

        private static void MoveFile(FileInfo source, DirectoryInfo destination)
        => File.Move(
               source.FullName,
               Path.Combine(destination.FullName,
               source.Name));

        private static void MoveDirectory(DirectoryInfo source, DirectoryInfo destination)
        => Directory.Move(
            source.FullName,
            Path.Combine(destination.FullName,
            source.Name));

        private static XDocument CreateNewCsprojDoc(Project project)
        {
            var root = new XElement(
                "Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"));

            #region  project info
            var propertyGroup = new XElement(
                "PropertyGroup",
                new XElement("TargetFramework", "net47"));
            root.Add(AppendNewLine(), propertyGroup);

            if (!ShouldDeleteAssemblyInfo(project.CsprojFile.Directory))
                propertyGroup.Add(new XElement("GenerateAssemblyInfo", "false"));

            project.Info
                    .GetType()
                    .GetProperties()
                    .Where(prop => prop.Name != nameof(ProjectInfo.AssemblyName))
                    .Select(prop => new { prop.Name, Value = prop.GetValue(project.Info) as string })
                    .Where(prop => prop.Value != null)
                    .ForAll(prop =>
                    {
                        if (prop.Name == "Product")
                            propertyGroup.Add(new XElement(prop.Name, "OBG Api"));

                        else if (prop.Name == "Description")
                            propertyGroup.Add(new XElement(prop.Name, $"{project.Info.AssemblyName} nuget package"));

                        else if (prop.Name == "Company")
                            propertyGroup.Add(new XElement(prop.Name, "Betsson Group"));

                        else if (prop.Name == "Authors")
                            propertyGroup.Add(new XElement(prop.Name, "OBG Api"));

                        else if (prop.Name == "PackageReleaseNotes")
                            return;

                        else if (prop.Name == "PackageProjectUrl")
                            return;

                        else
                            propertyGroup.Add(new XElement(prop.Name, prop.Value));
                    });
            propertyGroup.Add(AppendNewLine());
            #endregion

            #region  gac references
            var gacRefs = new XElement("ItemGroup");
            project.Assemblies.ForAll(@ref =>
            {
                gacRefs.Add(new XElement(
                    "Reference",
                    new XAttribute("Include", @ref.ProjectName)));
            });

            if (gacRefs.HasElements)
                root.Add(AppendNewLine(), gacRefs);
            #endregion

            #region  extenral references
            var nugetRefs = new XElement("ItemGroup");
            project.ExternalPackages.ForAll(@ref =>
            {
                nugetRefs.Add(new XElement(
                    "PackageReference",
                    new XAttribute("Include", @ref.ProjectName),
                    new XAttribute("Version", @ref.Version.LowerBound.Version.ToString())));
            });

            if (nugetRefs.HasElements)
                root.Add(AppendNewLine(), nugetRefs);
            #endregion

            #region vertical references
            var verticalRefs = new XElement("ItemGroup");
            project.Verticals.ForAll(@ref =>
            {
                verticalRefs.Add(new XElement(
                    "ProjectReference",
                    new XAttribute("Include", @ref.ProjectPath)));
            });

            if (verticalRefs.HasElements)
                root.Add(AppendNewLine(), verticalRefs);
            #endregion

            #region Horizontal references
            var horizontalPackageRefs = new XElement("ItemGroup");
            var horizontalProjectRefs = new XElement("ItemGroup");
            project.Horizontals.ForAll(@ref =>
            {
                var versionArg = @ref.Project.HorizontalVersionArg;
                horizontalPackageRefs.Add(new XElement(
                    "PackageReference",
                    new XAttribute("Include", @ref.ProjectName),
                    new XAttribute("Version", $"$({versionArg})"),
                    new XAttribute("Condition", " '$(BuildingInsideVisualStudio)' != 'true' ")));

                horizontalProjectRefs.Add(new XElement(
                    "ProjectReference",
                    new XAttribute("Include", @ref.ProjectPath),
                    new XAttribute("Condition", " '$(BuildingInsideVisualStudio)' == 'true' ")));

                propertyGroup.Add(new XElement(versionArg, @ref.Version.LowerBound.Version.ToString()));
            });

            if (horizontalPackageRefs.HasElements)
                root.Add(
                    AppendNewLine(), horizontalPackageRefs,
                    AppendNewLine(), horizontalProjectRefs);
            #endregion

            #region Plain References
            var plainRefs = new XElement("ItemGroup");
            project.PlainProjectRefs.ForAll(@ref =>
            {
                plainRefs.Add(new XElement(
                    "ProjectReference",
                    new XAttribute("Include", @ref.ProjectPath)));
            });

            if (plainRefs.HasElements)
                root.Add(AppendNewLine(), plainRefs);
            #endregion

            root.Add(AppendNewLine());

            return new XDocument(root);
        }

        public static object AppendNewLine()
        {
            return new XElement("NL");
        }


        public static void CreateYaml(FileInfo yamlFile, string vertical, Project[] projects)
        {
            var indent = new Func<int, string>(indents => Enumerable
                .Range(0, indents)
                .Select(index => "  ")
                .JoinUsing(""));

            var repos = ExtractDependentRepos(projects);

            var generateCheckouts = new Func<int, string>(indents =>
            {
                return repos
                    .Where(repo => repo != vertical)
                    .Select(repo =>
                    {
                        return $@"
{indent(indents)}- repository: ssh://git@bitbucketsson.betsson.local:7999/obg/{repo}.git
{indent(indents)}  branch: {(repo == "Obg.Core" ? "Before_Minor_Fixes_Meh" : "develop")}
{indent(indents)}  name: {repo}";
                    })
                    .JoinUsing("");
            });

            var generatePacks = new Func<int, string>(indents =>
            {
                return projects
                    .Where(project => !project.Info.AssemblyName.EndsWith("Tests"))
                    .Select(project =>
                    {
                        return $@"
{indent(indents)}- {project.Info.AssemblyName}";
                    })
                    .JoinUsing("");
            });

            yamlFile.CreateText().Using(stream =>
            {
                stream.Write(
$@"chain: dotnet-lib
dependencies:  
  checkoutDir: ../
  list:{generateCheckouts(1)}

pack:
  only:{generatePacks(2)}

  publish:
    default-feed: OBG
    feeds:
    - branch: refs/heads/master
      feed: OBG
    - branch: refs/heads/release/.+
      feed: OBG
    - branch: refs/heads/develop
      feed: OBG-Dev
    - branch: refs/heads/feature/.+
      feed: OBG-Dev
build:
  runtimes: []
");
            });
        }
        public static void CreateReadme(FileInfo readme)
        {
            readme.CreateText().Using(stream =>
            {
                stream.Write("\n");
                stream.Flush();
            });
        }
        public static void CreateChangelog(FileInfo changelog)
        {
            changelog.CreateText().Using(stream =>
            {
                stream.Write("\n");
                stream.Flush();
            });
        }

        public static bool ShouldDeleteAssemblyInfo(DirectoryInfo projectDir, out FileInfo assemblyInfoFile)
        {
            assemblyInfoFile = projectDir
                .ChildDirectory("Properties")
                .GetFiles("AssemblyInfo.cs")
                .FirstOrDefault();

            if (assemblyInfoFile == null) return true;

            return !assemblyInfoFile
                .OpenRead()
                .Using(stream =>
                {
                    var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    return content.Contains("InternalsVisibleTo");
                });
        }

        public static bool ShouldDeleteAssemblyInfo(DirectoryInfo projectDir) => ShouldDeleteAssemblyInfo(projectDir, out var aif);

        public static string[] ExtractDependentRepos(IEnumerable<Project> projects)
        {
            var repos = new HashSet<string>();
            projects.ForAll(project => ExtractRepos(project, repos));

            return repos.ToArray();
        }
        private static void ExtractRepos(Project project, HashSet<string> repos)
        {
            repos.Add(project.VerticalName());

            project.Verticals.ForAll(vr => ExtractRepos(vr.Project, repos));
            project.Horizontals.ForAll(hr => ExtractRepos(hr.Project, repos));
        }
    }
}
