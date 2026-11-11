using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TheVerge.Core;
using TheVerge.Data;
using TheVerge.Network.Compression;

namespace TheVerge.Tests.Architecture
{
    /// <summary>
    /// Правила зависимостей AGENTS.md §2.1 на уровне готовых сборок: даже если кто-то поправит .csproj,
    /// тест упадёт. В Unity то же самое сторожат .asmdef и tools/check/architecture.sh.
    /// Компилятор выкидывает неиспользуемые ссылки, поэтому проверяется «не больше разрешённого», а не равенство.
    /// </summary>
    public sealed class AssemblyReferencesTests
    {
        private static readonly Assembly CoreAssembly = typeof(Serial).Assembly;
        private static readonly Assembly NetworkAssembly = typeof(HuffmanDecoder).Assembly;
        private static readonly Assembly DataAssemblyRef = typeof(DataAssembly).Assembly;

        [Test]
        public void Core_DoesNotReferenceOtherProjectAssemblies()
        {
            Assert.That(ProjectReferences(CoreAssembly), Is.Empty);
        }

        [Test]
        public void Network_ReferencesOnlyCore()
        {
            Assert.That(ProjectReferences(NetworkAssembly), Is.SubsetOf(new[] { "TheVerge.Core" }));
        }

        [Test]
        public void Data_ReferencesOnlyCore()
        {
            Assert.That(ProjectReferences(DataAssemblyRef), Is.SubsetOf(new[] { "TheVerge.Core" }));
        }

        [TestCase("TheVerge.Core")]
        [TestCase("TheVerge.Network")]
        [TestCase("TheVerge.Data")]
        public void PureAssembly_DoesNotReferenceUnity(string assemblyName)
        {
            var assembly = new[] { CoreAssembly, NetworkAssembly, DataAssemblyRef }
                .Single(a => a.GetName().Name == assemblyName);

            var unityReferences = assembly.GetReferencedAssemblies()
                .Select(r => r.Name ?? string.Empty)
                .Where(n => n.StartsWith("Unity"));

            Assert.That(unityReferences, Is.Empty);
        }

        private static string[] ProjectReferences(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies()
                .Select(r => r.Name ?? string.Empty)
                .Where(n => n.StartsWith("TheVerge."))
                .ToArray();
        }
    }
}
