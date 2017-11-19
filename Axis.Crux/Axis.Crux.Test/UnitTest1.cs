using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Axis.Crux.MSBuildTarget;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Axis.Crux.Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var bv = new BranchVersioner
            {
                ProjectDirectory = @"C:\_dev\Cyberspace\Projects\FinTech\PayProcessor\Repos\Core\PayProcessor.Core",
                AssemblyName = "PayProcessor.Core",
                ProjectName = "PayProcessor.Core",
                OutputPath = "bin/debug"
            };

            bv.UpdateNuspec(new SemVer { Major = 1, Minor = 1, Patch = 4 });
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
            semver = new SemVer("1.2.32");
            semver = new SemVer("1.2.32-5");
            semver = new SemVer("1.2.32-pre-543");
            semver = new SemVer("1.2.32.5");
            semver = new SemVer("1.2.3 2 .5");
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
