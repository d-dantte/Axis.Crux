using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Axis.Crux.VSpec;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Axis.Crux.Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var json = JsonConvert.SerializeObject(new Options
            {
                Versions = new[]
                {
                    new PackageVersion
                    {
                        Version = new SemVerRange("2.1.4")
                    }
                }
            }, 
            Formatting.Indented,
            PackageVersioner.JsonSerializerSettings);
            Console.WriteLine(json);

            var options = JsonConvert.DeserializeObject<Options>(json, PackageVersioner.JsonSerializerSettings);
            Console.WriteLine(options.ToString());
        }

        [TestMethod]
        public void TestMethod2()
        {
            var regex = new Regex(@"\[\s*assembly\s*\:\s+AssemblyVersion\s*\(\s*\""\s*\d+(\.\d+){2}[^""]*\""\s*\)\s*\]");


            Assert.IsTrue(regex.IsMatch(@"[assembly: AssemblyVersion(""2.1.2"")]"));
        }

        [TestMethod]
        public void TestMethod3()
        {
            var semver = new SemVer("1.2.32");
            Console.WriteLine(semver);

            semver = new SemVer("1.2.32-pre");
            Console.WriteLine(semver);

            semver = new SemVer("1.2.32-dev-*");
            Console.WriteLine(semver);

            semver = new SemVer("1.2.32-pre-543564");
            Console.WriteLine(semver);

            semver = new SemVer("1.2.32-pre.65456.6545-64-654356");
            Console.WriteLine(semver);
        }

        [TestMethod]
        public void SemVerRangeTests()
        {
            var semverString = "1.2.32";
            var semver = new SemVerRange(semverString);
            Assert.AreEqual(semverString, semver.ToString());
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.AreEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version.ToString());
            Assert.IsNull(semver.LowerBound.Version.Pre);

            semverString = "1.2.32-pre";
            semver = new SemVerRange(semverString);
            Assert.AreEqual(semverString, semver.ToString());
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.AreEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version.ToString());
            Assert.AreEqual("pre", semver.LowerBound.Version.Pre);

            semverString = "1.2.32-beta-*";
            semver = new SemVerRange(semverString);
            Assert.IsTrue(semver.ToString().StartsWith(semverString.TrimEnd('*')));
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.AreEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version.ToString());
            Assert.IsTrue(semver.LowerBound.Version.Pre?.Contains("beta") == true);

            semverString = "1.2.32-pre-543564";
            semver = new SemVerRange(semverString);
            Assert.AreEqual(semverString, semver.ToString());
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.AreEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version.ToString());
            Assert.AreEqual(semver.LowerBound.Version.Pre, "pre-543564");

            semverString = "1.2.32-pre.65456.6545-64-654356";
            semver = new SemVerRange(semverString);
            Assert.AreEqual(semverString, semver.ToString());
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.AreEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version.ToString());
            Assert.AreEqual(semver.LowerBound.Version.Pre, "pre.65456.6545-64-654356");

            semverString = "(1.2.32,)";
            semver = new SemVerRange(semverString);
            Assert.AreEqual(semverString, semver.ToString());
            Assert.AreNotEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version?.ToString());
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.IsNull(semver.LowerBound.Version.Pre);
            Assert.AreEqual(SemVerBoundaryType.Exclusive, semver.LowerBound.BoundaryType);
            Assert.AreEqual(SemVerBoundaryType.Exclusive, semver.UpperBound.BoundaryType);
            Assert.IsNull(semver.UpperBound.Version);

            semverString = "[1.2.32-alpha,)";
            semver = new SemVerRange(semverString);
            Assert.AreEqual(semverString, semver.ToString());
            Assert.AreNotEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version?.ToString());
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.AreEqual(semver.LowerBound.Version.Pre, "alpha");
            Assert.AreEqual(SemVerBoundaryType.Inclusive, semver.LowerBound.BoundaryType);
            Assert.AreEqual(SemVerBoundaryType.Exclusive, semver.UpperBound.BoundaryType);
            Assert.IsNull(semver.UpperBound.Version);


            semverString = "[1.2.32-alpha,2.5.3)";
            semver = new SemVerRange(semverString);
            Assert.AreEqual(semverString, semver.ToString());
            Assert.AreNotEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version.ToString());
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.AreEqual(semver.LowerBound.Version.Pre, "alpha");
            Assert.AreEqual(SemVerBoundaryType.Inclusive, semver.LowerBound.BoundaryType);
            Assert.AreEqual(2, (int)semver.UpperBound.Version.Major);
            Assert.AreEqual(5, (int)semver.UpperBound.Version.Minor);
            Assert.AreEqual(3, (int)semver.UpperBound.Version.Patch);
            Assert.IsNull(semver.UpperBound.Version.Pre);
            Assert.AreEqual(SemVerBoundaryType.Exclusive, semver.UpperBound.BoundaryType);


            semverString = "[1.2.32-alpha,2.5.3-beta]";
            semver = new SemVerRange(semverString);
            Assert.AreEqual(semverString, semver.ToString());
            Assert.AreNotEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version.ToString());
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.AreEqual(semver.LowerBound.Version.Pre, "alpha");
            Assert.AreEqual(SemVerBoundaryType.Inclusive, semver.LowerBound.BoundaryType);
            Assert.AreEqual(2, (int)semver.UpperBound.Version.Major);
            Assert.AreEqual(5, (int)semver.UpperBound.Version.Minor);
            Assert.AreEqual(3, (int)semver.UpperBound.Version.Patch);
            Assert.AreEqual(semver.UpperBound.Version.Pre, "beta");
            Assert.AreEqual(SemVerBoundaryType.Inclusive, semver.UpperBound.BoundaryType);


            semverString = "[1.2.32-alpha]";
            semver = new SemVerRange(semverString);
            Assert.AreEqual(semverString, semver.ToString());
            Assert.AreEqual(semver.LowerBound.Version.ToString(), semver.UpperBound.Version.ToString());
            Assert.AreEqual(1, (int)semver.LowerBound.Version.Major);
            Assert.AreEqual(2, (int)semver.LowerBound.Version.Minor);
            Assert.AreEqual(32, (int)semver.LowerBound.Version.Patch);
            Assert.AreEqual(semver.LowerBound.Version.Pre, "alpha");
            Assert.AreEqual(SemVerBoundaryType.Inclusive, semver.LowerBound.BoundaryType);
            Assert.AreEqual(SemVerBoundaryType.Inclusive, semver.UpperBound.BoundaryType);

            try
            {
                semver = new SemVerRange("(1.2.32-alpha)"); //<-- should fail
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }



        [TestMethod]
        public void TestMethod4()
        {
            var xml = @"<root><e1><e2><e3/></e2></e1></root>";

            var xdoc = XDocument.Parse(xml);
            var elt = xdoc.Element("root").Element("brach");
        }
    }
}
