using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Obg.MonoRepoMigration.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var version = SemVerRange.Parse("[1.1.11,1.2.0)");
            Console.WriteLine(version.ToString());
            Console.WriteLine(version.LowerBound.Version.ToString());
            Console.WriteLine(version.UpperBound.Version.ToString());
            Assert.AreEqual("1.1.11", version.LowerBound.Version.ToString());
        }
    }
}
