using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Obg.MonoRepoMigration
{
    public class Project
    {
        public FileInfo CsprojFile { get; set; }
        public XDocument CsprojDoc { get; set; }
        public bool? IsNewFormat => CsprojDoc?.IsMonoRepoFormat();
        public string VerticalName()
        => CsprojFile.ResolveVerticalName();

        public string HorizontalVersionArg { get; private set; }

        public ProjectInfo Info { get; } = new ProjectInfo();
        public List<ObgDependencyRef> Horizontals { get; } = new List<ObgDependencyRef>();
        public List<ObgDependencyRef> Verticals { get; } = new List<ObgDependencyRef>();
        public List<AssemblyDependencyRef> Assemblies { get; } = new List<AssemblyDependencyRef>();
        public List<PackageDependencyRef> ExternalPackages { get; } = new List<PackageDependencyRef>();
        public List<PlainProjectRef> PlainProjectRefs { get; } = new List<PlainProjectRef>();


        public Project(FileInfo csprojFile, DependencyContext context)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(csprojFile.Directory.FullName);

                if (!csprojFile.Exists)
                    throw new System.Exception("Invalid file specified");

                if (csprojFile.Extension.ToLower() != ".csproj")
                    throw new System.Exception("Invalid csproj file extension");

                CsprojFile = csprojFile;
                CsprojDoc = csprojFile.OpenRead().Using(XDocument.Load);
                HorizontalVersionArg = ResolveHorizontalVersionArg(CsprojFile);

                if (CsprojDoc.IsMonoRepoFormat())
                    BuildFromNewCsproj();

                else
                    BuildFromOldCsproj();

                //context.ProjectTable.Add(Info.AssemblyName, this);
                ResolveDependentProjects(context);
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        public HashSet<string> FlatProjectDependency()
        {
            var hashSet = new HashSet<string>();
            Horizontals.ForAll(h =>
            {
                hashSet.Add(h.ProjectName);
                h.Project
                    .FlatProjectDependency()
                    .ForAll(hfpd => hashSet.Add(hfpd));
            });
            Verticals.ForAll(v =>
            {
                hashSet.Add(v.ProjectName);
                v.Project
                    .FlatProjectDependency()
                    .ForAll(vfpd => hashSet.Add(vfpd));
            });
            PlainProjectRefs.ForAll(p =>
            {
                hashSet.Add(p.ProjectName);
                p.Project
                    .FlatProjectDependency()
                    .ForAll(pfpd => hashSet.Add(pfpd));
            });

            return hashSet;
        }

        public override string ToString() => Info.AssemblyName;

        private void BuildFromOldCsproj()
        {
            var nuspecFile = new FileInfo(Ext.ResolveNuspecPath(CsprojFile));
            var nuspecDoc = nuspecFile.Exists
                ? nuspecFile.OpenRead().Using(XDocument.Load)
                : null;

            #region Generate project Info
            Info.AssemblyName = 
                nuspecDoc?.Root.Find("metadata/id").Value
                ?? CsprojDoc.Root.Find("PropertyGroup/AssemblyName").Value;

            var textInfo = new CultureInfo("en-US", false).TextInfo;
            Info.Product = textInfo.ToTitleCase(Info.AssemblyName);

            Info.Authors = "OBG Api";

            Info.Company = 
                nuspecDoc?.Root.Find("metadata/owners")?.Value
                ?? "Betsson Group";

            Info.Copyright = $"Copyright © {DateTimeOffset.Now.Year}";

            Info.Description = nuspecDoc?.Root.Find("metadata/description")?.Value;

            Info.PackageReleaseNotes = nuspecDoc?.Root.Find("metadata/releaseNotes")?.Value;

            Info.PackageTags = nuspecDoc?.Root.Find("metadata/tags")?.Value;

            Info.PackageProjectUrl = nuspecDoc?.Root.Find("metadata/projectUrl")?.Value;

            Info.PackageIconUrl = nuspecDoc?.Root.Find("metadata/iconUrl")?.Value;
            #endregion

            //dependencies
            //1. assembly references
            CsprojDoc.Root
                .FindAll("ItemGroup/Reference")
                .Where(IsGacReference)
                .ForAll(element => Assemblies.Add(new AssemblyDependencyRef
                {
                    ProjectName = element.Attribute("Include").Value
                }));

            //2. external packages
            CsprojDoc.Root
                .FindAll("ItemGroup/Reference")
                .Where(IsExternalNuspecDependency)
                .ForAll(element =>
                {
                    var assembly = element.Attribute("Include").Value
                        .Split(',')
                        .First();

                    var dependency = nuspecDoc?.Root
                        .FindAll(
                            "metadata/dependencies/dependency",
                            "metadata/dependencies/group/dependency")
                        .FirstOrDefault(Ext.HasAttribute("id", assembly));

                    if (dependency != null)
                    {
                        var version = dependency.Attribute("version").Value;
                        ExternalPackages.Add(new PackageDependencyRef
                        {
                            ProjectName = assembly,
                            Version = string.IsNullOrWhiteSpace(version) ? null : SemVerRange.Parse(version)
                        });
                    }

                    //else this is a transitive dependency; ignore it.
                });

            //3. horizontal project reference
            //check the project references and determine, using the solution name, which are, and which aren't
            //horizontal references. Then check the nuspec file to determine the version to use
            CsprojDoc.Root
                .FindAll("ItemGroup/ProjectReference")
                .Where(IsOldHorizontalProjectReference)
                .ForAll(element =>
                {
                    var path = element.Attribute("Include").Value;
                    var assembly = path.ExtractProjectNameFromFilePath();
                    var dependency = nuspecDoc?.Root
                        .FindAll(
                            "metadata/dependencies/group/dependency",
                            "metadata/dependencies/dependency")
                        .FirstOrDefault(Ext.HasAttribute("id", assembly));

                    if (dependency != null)
                    {
                        var version = dependency
                            .Attribute("version")
                            .Value;

                        Horizontals.Add(new ObgDependencyRef
                        {
                            ProjectPath = path,
                            ProjectName = assembly,
                            Version = string.IsNullOrWhiteSpace(version) ? null : SemVerRange.Parse(version)
                        });
                    }

                    //else it is a transitive dependency; ignore it.
                    //{
                    //    PlainProjectRefs.Add(new PlainProjectRef
                    //    {
                    //        ProjectName = assembly,
                    //        ProjectPath = path
                    //    });
                    //}
                });

            //4. vertical project references
            //check the project references and determine, using the solution name, which are, and whicha aren't
            //vertical references.
            CsprojDoc.Root
                .FindAll("ItemGroup/ProjectReference")
                .Where(IsOldVerticalProjectReference)
                .ForAll(element =>
                {
                    var path = element.Attribute("Include").Value;
                    var projectName = path.ExtractProjectNameFromFilePath();
                    Verticals.Add(new ObgDependencyRef
                    {
                        ProjectPath = path,
                        ProjectName = projectName
                    });
                });
        }

        private void BuildFromNewCsproj()
        {
            var propGroup = CsprojDoc.Root
                .FindAll("PropertyGroup")
                .First(Ext.ContainsProjectInfo);

            //build info
            typeof(ProjectInfo)
                .GetProperties()
                .Select(prop => new { Prop = prop, Element = propGroup.Find(prop.Name) })
                .Where(map => map.Element != null)
                .ForAll(map => map.Prop.SetValue(Info, map.Element.Value));
            Info.AssemblyName = CsprojFile.Name.TrimEnd(CsprojFile.Extension);

            //build dependencies
            //1. assembly references
            CsprojDoc.Root
                .FindAll("ItemGroup/Reference")
                .ForAll(element => Assemblies.Add(new AssemblyDependencyRef
                {
                    ProjectName = element.Attribute("Include").Value
                }));

            //2. external packages
            CsprojDoc.Root
                .FindAll("ItemGroup/PackageReference")
                .Where(IsExternalPackageReference)
                .ForAll(element => ExternalPackages.Add(new PackageDependencyRef
                {
                    ProjectName = element.Attribute("Include").Value,
                    Version = SemVerRange.Parse(element.Attribute("Version").Value)
                }));

            //3. horizontal project references
            CsprojDoc.Root
                .FindAll("ItemGroup/ProjectReference")
                .Where(IsHorizontalProjectReference)
                .ForAll(element =>
                {
                    var path = element.Attribute("Include").Value;
                    var projectName = path.ExtractProjectNameFromFilePath();
                    var versionArg = ResolveHorizontalVersionArg(new FileInfo(path));
                    Horizontals.Add(new ObgDependencyRef
                    {
                        ProjectPath = path,
                        ProjectName = projectName,
                        Version = SemVerRange.Parse(propGroup.Find(versionArg).Value)
                    });
                });

            //4. vertical project references
            CsprojDoc.Root
                .FindAll("ItemGroup/ProjectReference")
                .Where(IsVerticalProjectReference)
                .ForAll(element =>
                {
                    var path = element.Attribute("Include").Value;
                    var projectName = path.ExtractProjectNameFromFilePath();
                    Verticals.Add(new ObgDependencyRef
                    {
                        ProjectPath = path,
                        ProjectName = projectName
                    });
                });

            //5. plain project reference
            CsprojDoc.Root
                .FindAll("ItemGroup/ProjectReference")
                .Where(IsPlainProjectReference)
                .ForAll(element =>
                {
                    var path = element.Attribute("Include").Value;
                    var projectName = path.ExtractProjectNameFromFilePath();
                    PlainProjectRefs.Add(new PlainProjectRef
                    {
                        ProjectPath = path,
                        ProjectName = projectName
                    });
                });
        }

        private bool IsGacReference(XElement element)
        {
            return element != null
                && !element.HasElements
                && element.Attributes().Count() == 1;
        }

        private bool IsExternalNuspecDependency(XElement element)
        {
            return element != null
                && element.HasAttribute("Include", out var att) && att.Value.Contains("Version")
                && element.HasChild("HintPath", out var child) && child.Value.ContainsAll("\\packages\\", ".dll");
        }

        private bool IsOldHorizontalProjectReference(XElement element)
        {
            var referencedProject = new FileInfo(element.Attribute("Include").Value);
            return referencedProject.Directory.Parent.FullName !=
                CsprojFile.Directory.Parent.FullName;
        }

        private bool IsOldVerticalProjectReference(XElement element)
        {
            var referencedProject = new FileInfo(element.Attribute("Include").Value);
            return referencedProject.Directory.Parent.FullName ==
                CsprojFile.Directory.Parent.FullName;
        }


        private bool IsExternalPackageReference(XElement reference)
        {
            return !reference.HasAttribute("Condition");
        }

        private bool IsHorizontalProjectReference(XElement reference)
        {
            return reference.HasAttribute("Condition")
                && reference.HasAttribute("Include", out var include)
                && new FileInfo(include.Value).Directory.Parent.FullName !=
                   CsprojFile.Directory.Parent.FullName;
        }

        private bool IsVerticalProjectReference(XElement reference)
        {
            return reference.HasAttribute("Condition")
                && reference.HasAttribute("Include", out var include)
                && new FileInfo(include.Value).Directory.Parent.FullName ==
                   CsprojFile.Directory.Parent.FullName;
        }

        private bool IsPlainProjectReference(XElement reference)
        {
            return !reference.HasAttribute("Condition")
                && reference.HasAttribute("Include");
        }

        private void ResolveDependentProjects(DependencyContext context)
        {
            //first resolve the projects
            Verticals.ForAll(v =>
            {
                v.Project = context.ProjectTable.GetOrAdd(
                    v.ProjectName, 
                    _ => new Project(new FileInfo(v.ProjectPath), context));
            });
            Horizontals.ForAll(h =>
            {
                h.Project = context.ProjectTable.GetOrAdd(
                    h.ProjectName,
                    _ => new Project(new FileInfo(h.ProjectPath), context));
            });
            PlainProjectRefs.ForAll(p =>
            {
                p.Project = context.ProjectTable.GetOrAdd(
                    p.ProjectName,
                    _ => new Project(new FileInfo(p.ProjectPath), context));
            });

            //now remove transient references
            var refs = new HashSet<string>(Horizontals
                .SelectMany(h => h.Project.FlatProjectDependency())
                .Concat(Verticals
                .SelectMany(v => v.Project.FlatProjectDependency())
                .Concat(PlainProjectRefs
                .SelectMany(p => p.Project.FlatProjectDependency()))));

            Verticals.ToArray().ForAll(v =>
            {
                if (refs.Contains(v.ProjectName))
                    Verticals.Remove(v);
            });

            Horizontals.ToArray().ForAll(h =>
            {
                if (refs.Contains(h.ProjectName))
                    Horizontals.Remove(h);
            });

            PlainProjectRefs.ToArray().ForAll(p =>
            {
                if (refs.Contains(p.ProjectName))
                    PlainProjectRefs.Remove(p);
            });
        }

        private static string ResolveHorizontalVersionArg(FileInfo csprojFile)
        {
            var csprojDoc = csprojFile.OpenRead().Using(XDocument.Load);

            if (csprojDoc.IsMonoRepoFormat())
                return $"{csprojFile.ResolveVerticalName().Replace(".", "")}Version";

            else
                return $"{csprojFile.FullName.ExtractProjectNameFromFilePath().Replace(".", "")}Version";
        }
    }

    public class DependencyContext
    {
        public Dictionary<string, Project> ProjectTable { get; }
        //public HashSet<HierarchyRef> DependencyGraph { get; } = new HashSet<HierarchyRef>();

        public DependencyContext(Dictionary<string, Project> projectTable)
        {
            ProjectTable = projectTable ?? throw new System.Exception("");
        }
    }

    public class ProjectInfo
    {
        public string TargetFramework { get; set; }
        public string Product { get; set; }
        public string AssemblyName { get; set; }
        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Company { get; set; }
        public string Description { get; set; }
        public string PackageReleaseNotes { get; set; }
        public string PackageTags { get; set; }
        public string PackageIconUrl { get; set; }
        public string PackageProjectUrl { get; set; }
    }

    public abstract class DependencyRef
    {
        public string ProjectName { get; set; }
    }

    public class AssemblyDependencyRef : DependencyRef
    {
        public override string ToString() => $"{ProjectName}";
    }

    public class ObgDependencyRef : DependencyRef
    {
        public SemVerRange Version { get; set; }
        public Project Project { get; set; }
        public string ProjectPath { get; set; }


        public override string ToString() => $"{ProjectPath}@{Version?.ToString() ?? "-.-.-"}";
    }

    public class PlainProjectRef: DependencyRef
    {
        public Project Project { get; set; }
        public string ProjectPath { get; set; }
        public override string ToString() => ProjectPath ?? "Unresolved";
    }

    public class PackageDependencyRef : DependencyRef
    {
        public SemVerRange Version { get; set; }

        public override string ToString() => $"{ProjectName}@{Version?.ToString() ?? "-.-.-"}";
    }
}
