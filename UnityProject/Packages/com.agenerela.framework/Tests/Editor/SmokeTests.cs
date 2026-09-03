using NUnit.Framework;

namespace Agenerela.Tests
{
    /// <summary>
    /// Verifies the package's assemblies resolve and are visible to the test runner.
    /// Deliberately trivial — its job is to prove the assembly-definition wiring is
    /// correct before any real code exists, so a later failure points at the code
    /// rather than at the project setup.
    /// </summary>
    public class SmokeTests
    {
        [Test]
        public void RuntimeAssemblyIsReferenced()
        {
            Assert.That(AgenerelaInfo.PackageName, Is.EqualTo("com.agenerela.framework"));
        }

        [Test]
        public void VersionIsPopulated()
        {
            Assert.That(AgenerelaInfo.Version, Is.Not.Null.And.Not.Empty);
        }
    }
}
