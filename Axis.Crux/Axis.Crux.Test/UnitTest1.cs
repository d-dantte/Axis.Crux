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
                IsAutoAssemblyCopyDisabled = true,
                Versions = new[]
                {
                    new PackageVersion
                    {
                        Version = new SemVer("2.1.4")
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

            semver = new SemVer("1.2.32-pre-543564");
            Console.WriteLine(semver);

            semver = new SemVer("1.2.32-pre-65456-6545-64-654356");
            Console.WriteLine(semver);
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
