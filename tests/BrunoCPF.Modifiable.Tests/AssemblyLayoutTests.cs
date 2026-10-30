using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BrunoCPF.Modifiable.Common.Math;
using BrunoCPF.Modifiable.Common.Properties;
using BrunoCPF.Modifiable.Core;
using NUnit.Framework;

namespace BrunoCPF.Modifiable.Tests
{
    /// <summary>
    /// Guards the split that lets engine-free code use the core: BrunoCPF.Modifiable.Core references
    /// neither R3 nor UnityEngine, in the built NuGet assembly and in the Unity asmdefs.
    /// </summary>
    public class AssemblyLayoutTests
    {
        private static readonly string[] _forbidden = { "R3", "UnityEngine", "UnityEditor" };

        [TestCase(typeof(ModifierStack))]
        [TestCase(typeof(DeltaPipeline))]
        [TestCase(typeof(Modifier<>))]
        [TestCase(typeof(DeltaFilter<>))]
        [TestCase(typeof(ValueMath))]
        [TestCase(typeof(ValueBounds<>))]
        public void PureTypes_LiveInTheCoreAssembly(Type type)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("BrunoCPF.Modifiable.Core"));
        }

        [TestCase(typeof(ModifiableProperty<,,>))]
        [TestCase(typeof(IReadOnlyModifiableProperty<,>))]
        public void ReactiveTypes_LiveInTheReactiveAssembly(Type type)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("BrunoCPF.Modifiable"));
        }

        [Test]
        public void CoreAssembly_ReferencesNeitherR3NorUnity()
        {
            string[] references = typeof(ModifierStack).Assembly.GetReferencedAssemblies()
                .Select(name => name.Name!)
                .ToArray();

            Assert.That(references, Has.None.Matches<string>(IsForbidden), string.Join(", ", references));
        }

        [Test]
        public void CoreProject_HasNoDependencies()
        {
            string project = File.ReadAllText(RepoPath("src/BrunoCPF.Modifiable.Core/BrunoCPF.Modifiable.Core.csproj"));

            Assert.That(project, Does.Not.Contain("PackageReference").And.Not.Contain("ProjectReference"));
        }

        [Test]
        public void CoreAsmdef_IsEngineFreeWithNoReferences()
        {
            JsonElement asmdef = ReadAsmdef("BrunoCPF.Modifiable.Core/BrunoCPF.Modifiable.Core.asmdef");

            Assert.That(asmdef.GetProperty("name").GetString(), Is.EqualTo("BrunoCPF.Modifiable.Core"));
            Assert.That(asmdef.GetProperty("noEngineReferences").GetBoolean(), Is.True);
            Assert.That(asmdef.GetProperty("overrideReferences").GetBoolean(), Is.True);
            Assert.That(asmdef.GetProperty("references").GetArrayLength(), Is.Zero);
            Assert.That(asmdef.GetProperty("precompiledReferences").GetArrayLength(), Is.Zero);
        }

        [Test]
        public void ReactiveAsmdef_IsEngineFreeAndUsesOnlyR3Core()
        {
            JsonElement asmdef = ReadAsmdef("BrunoCPF.Modifiable/BrunoCPF.Modifiable.asmdef");

            Assert.That(asmdef.GetProperty("noEngineReferences").GetBoolean(), Is.True);
            Assert.That(asmdef.GetProperty("overrideReferences").GetBoolean(), Is.True);
            Assert.That(Strings(asmdef, "references"), Is.EqualTo(new[] { "BrunoCPF.Modifiable.Core" }));
            Assert.That(Strings(asmdef, "precompiledReferences"), Is.EqualTo(new[] { "R3.dll" }));
        }

        [Test]
        public void CoreSources_DoNotUseR3OrUnity()
        {
            string[] offenders = Directory
                .EnumerateFiles(RepoPath("unity/Runtime/BrunoCPF.Modifiable.Core"), "*.cs", SearchOption.AllDirectories)
                .Where(file => File.ReadLines(file).Any(line =>
                    line.StartsWith("using ", StringComparison.Ordinal) && IsForbidden(line.Substring(6).TrimEnd(';'))))
                .ToArray();

            Assert.That(offenders, Is.Empty);
        }

        private static bool IsForbidden(string name) =>
            _forbidden.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal));

        private static JsonElement ReadAsmdef(string relativePath) =>
            JsonDocument.Parse(File.ReadAllText(RepoPath("unity/Runtime/" + relativePath))).RootElement;

        private static string[] Strings(JsonElement element, string property) =>
            element.GetProperty(property).EnumerateArray().Select(item => item.GetString()!).ToArray();

        private static string RepoPath(string relativePath, [CallerFilePath] string thisFile = "") =>
            Path.Combine(Path.GetDirectoryName(thisFile)!, "../..", relativePath);
    }
}
