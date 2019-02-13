using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Obg.MonoRepoMigration
{
    using Microsoft.Build.Construction;
    using System.Globalization;
    using System.Xml;
    using static Ext;

//    public class Program
//    {
//        static void Main(string[] args)
//        {
//            //starting from a solution folder...
//            Console.Write("Enter Vertical Directory: ");

//            var path = Console.ReadLine();
//            var directory = new DirectoryInfo(path);
//            Dependencies dep = new Dependencies();

//            if (!directory.Exists)
//                Console.WriteLine("Invalid directory specified\nExiting...");

//            else
//                directory
//                    .EnumerateDirectories()
//                    .Select(sub => sub.EnumerateFiles("*.csproj").FirstOrDefault())
//                    .Where(Ext.IsNotNull)
//                    .ForAll(finfo =>
//                    {
//                        if (!dep.Merge(MigrateCsProj(finfo)))
//                            Console.WriteLine($"Failed to Migrate project: {finfo.FullName}");

//                        else
//                        {
//                            Console.WriteLine($"Migration Successful for: {Ext.ExtractNewCsprojName(finfo)}");
//                        }
//                    });

//            #region Create solution folder files
//            //create obg-build.yaml
//            CreateYaml(new FileInfo(directory.FullName + "/obg-build.yaml"), dep);

//            //create changelog.md
//            CreateChangelog(new FileInfo(directory.FullName + "/CHANGELOG.md"));

//            //create readme.md
//            CreateChangelog(new FileInfo(directory.FullName + "/README.md"));
//            #endregion

//            Console.WriteLine("Press any key to exit...");
//            Console.ReadKey();
//        }

//        public static Dependencies MigrateCsProj(FileInfo oldcsprojFile)
//        {
//            var originalAppDirectory = Directory.GetCurrentDirectory();
//            var deps = new Dependencies();
//            try
//            {
//                //set the current directory
//                Directory.SetCurrentDirectory(oldcsprojFile.Directory.FullName);

//                var oldcsprojDoc = new FileStream(oldcsprojFile.FullName, FileMode.Open).Using(stream => XDocument.Load(stream));
//                if (Ext.IsMonoRepoFormat(oldcsprojDoc))
//                    return new Dependencies();

//                var oldnuspec = new FileStream(Ext.ResolveNuspecPath(oldcsprojFile), FileMode.Open).Using(stream => XDocument.Load(stream));

//                XElement nuspecPropGroup = null;
//                var newcsproj = new XDocument(
//                    new XElement(
//                        "Project",
//                        new XAttribute(
//                            "Sdk",
//                            "Microsoft.NET.Sdk"),
//                        nuspecPropGroup = new XElement("PropertyGroup")));

//                //target framework
//                nuspecPropGroup.Add(new XElement("TargetFramework", "net47"));

//                //generate assembly info
//                nuspecPropGroup.Add(new XElement("GenerateAssemblyInfo", "false"));

//                //version?
//                //leave out the version

//                //product
//                var elt = oldnuspec.Root.Find("metadata/id");
//                var textInfo = new CultureInfo("en-US", false).TextInfo;
//                nuspecPropGroup.Add(new XElement("Product", textInfo.ToTitleCase(elt.Value.Replace(".", " "))));

//                //Authors
//                nuspecPropGroup.Add(new XElement("Authors", "OBG Api"));

//                //copyright
//                nuspecPropGroup.Add(new XElement("Copyright", $"Copyright © {DateTimeOffset.Now.Year}"));

//                //Company
//                elt = oldnuspec.Root.Find("metadata/owners");
//                if (elt != null)
//                    nuspecPropGroup.Add(new XElement("Company", elt.Value));

//                //description
//                elt = oldnuspec.Root.Find("metadata/description");
//                if (elt != null)
//                    nuspecPropGroup.Add(new XElement("Description", elt.Value?.Replace("$version$", "")));

//                //release notes
//                elt = oldnuspec.Root.Find("metadata/releaseNotes");
//                if (elt != null)
//                    nuspecPropGroup.Add(new XElement("PackageReleaseNotes", elt.Value));

//                //tags
//                elt = oldnuspec.Root.Find("metadata/tags");
//                if (elt != null)
//                    nuspecPropGroup.Add(new XElement("PackageTags", elt.Value));

//                //project url
//                elt = oldnuspec.Root.Find("metadata/projectUrl");
//                if (elt != null)
//                    nuspecPropGroup.Add(new XElement("PackageProjectUrl", elt.Value));

//                //icon url
//                elt = oldnuspec.Root.Find("metadata/iconUrl");
//                if (elt != null)
//                    nuspecPropGroup.Add(new XElement("PackageIconUrl", elt.Value));

//                //remove assemblyinfo
//                newcsproj.Root.Add(new XElement(
//                    "ItemGroup",
//                    new XElement(
//                        "Compile",
//                        new XAttribute("Remove", "Properties\\AssemblyInfo.cs"))));


//                //dependencies
//                //copy all root dependencies from nuspec
//                var transformNuspecDependency = Ext.TransformNuspecDependency(oldcsprojDoc, oldcsprojFile, deps);
//                XElement verticalPrjRef = new XElement("ItemGroup"),
//                         purePkgRef = new XElement("ItemGroup"),
//                         horizontalPrjRef = new XElement("ItemGroup"),
//                         horizontalPkgRef = new XElement("ItemGroup"),
//                         gacAssemblyRef = new XElement("ItemGroup");

//                oldnuspec.Root
//                    .FindAll(
//                        "metadata/dependencies/group/dependency",
//                        "metadata/dependencies/dependency")
//                    .Select(transformNuspecDependency)
//                    .Where(Ext.IsNotNull)
//                    .ForAll(reference =>
//                    {
//                        if (reference.Name == "PackageReference") //pure package ref
//                            purePkgRef.Add(reference);

//                        else if (reference.Name == "ProjectReference")
//                            verticalPrjRef.Add(reference);

//                        else  if(reference.Name == "HorizontalReference")
//                        {
//                            horizontalPkgRef.Add(reference.Find("PackageReference"));
//                            horizontalPrjRef.Add(reference.Find("ProjectReference"));
//                            nuspecPropGroup.Add(new XElement(
//                                reference.Attribute("VersionRef").Value,
//                                reference.Attribute("Version").Value));
//                        }

//                        else if(reference.Name == "GacReference")
//                        {
//                            //include gac referenes inside their own ItemGroup
//                        }
//                    });

//                newcsproj.Root.Add(
//                    purePkgRef.HasElements ? purePkgRef : null,
//                    verticalPrjRef.HasElements ? verticalPrjRef : null,
//                    horizontalPrjRef.HasElements ? horizontalPrjRef : null,
//                    horizontalPkgRef.HasElements ? horizontalPkgRef : null);

//                //rename the old csproj
//                File.Move(oldcsprojFile.FullName, $"{oldcsprojFile.FullName}.old");

//                //output the new csproj
//                using (var writer = XmlWriter.Create(
//                    new FileStream(oldcsprojFile.FullName, FileMode.Create),
//                    new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
//                {
//                    newcsproj.WriteTo(writer);
//                    writer.Flush();
//                }

//                //delete obj folder
//                var objDir = oldcsprojFile.Directory
//                    .GetDirectories()
//                    .Where(dir => dir.Name == "obj")
//                    .FirstOrDefault();

//                if (objDir?.Exists == true)
//                    objDir.Delete(true);

//                //delete changelog.md
//                //delete readme.md
//                //delete app.config
//                //delete nuspec
//                //delete package.config

//                //Add the Vertical name, not the individual solution names
//                deps.Converts.Add(oldcsprojFile.VerticalName());
//                return deps;
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine(e.Message);
//                Console.WriteLine(e.StackTrace);

//                return null;
//            }
//            finally
//            {
//                Directory.SetCurrentDirectory(originalAppDirectory);
//            }
//        }

//        public static void CreateYaml(FileInfo yamlFile, Dependencies deps)
//        {

//            var indent = new Func<int, string>(indents => Enumerable
//                .Range(0, indents)
//                .Select(index => "  ")
//                .JoinUsing(""));

//            var generateCheckouts = new Func<int, string>(indents =>
//            {
//                return deps
//                    .Horizontals
//                    .Select(dep =>
//                    {
//                        return $@"
//{indent(indents)}- repository: ssh://git@bitbucketsson.betsson.local:7999/obg/{dep}.git
//{indent(indents)}  branch: {(dep == "Obg.Core"? "Before_Minor_Fixes_Meh" : "develop")}
//{indent(indents)}  name: {dep}";
//                    })
//                    .JoinUsing("");
//            });

//            var generatePacks = new Func<int, string>(indents =>
//            {
//                return deps
//                    .Converts
//                    .Select(dep =>
//                    {
//                        return $@"
//{indent(indents)}- {dep}";
//                    })
//                    .JoinUsing("");
//            });

//            yamlFile.CreateText().Using(stream =>
//            {
//                stream.Write(
//$@"chain: dotnet-lib
//dependencies:  
//  checkoutDir: ../
//  list:{generateCheckouts(1)}

//pack:
//  only:{generatePacks(2)}

//  publish:
//    default-feed: OBG
//    feeds:
//    - branch: refs/heads/master
//      feed: OBG
//    - branch: refs/heads/release/.+
//      feed: OBG
//    - branch: refs/heads/develop
//      feed: OBG-Dev
//    - branch: refs/heads/feature/.+
//      feed: OBG-Dev
//build:
//  runtimes: []
//");
//            });
//        }
//        public static void CreateReadme(FileInfo readme)
//        {
//            readme.CreateText().Using(stream =>
//            {
//                stream.Write("\n");
//                stream.Flush();
//            });
//        }
//        public static void CreateChangelog(FileInfo changelog)
//        {
//            changelog.CreateText().Using(stream =>
//            {
//                stream.Write("\n");
//                stream.Flush();
//            });
//        }
//    }

//    public class Dependencies
//    {
//        public HashSet<string> Horizontals { get; set; } = new HashSet<string>();
//        public HashSet<string> Converts { get; set; } = new HashSet<string>();

//        public bool Merge(Dependencies dep)
//        {
//            if (dep == null) return false;
//            else
//            {
//                dep.Horizontals.ForAll(d => Horizontals.Add(d));
//                dep.Converts.ForAll(d => Converts.Add(d));

//                return true;
//            }
//        }
//    }
}
