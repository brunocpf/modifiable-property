using System;
using System.Linq;
using System.Collections.Generic;
using BrunoCPF.Modifiable.Common.Math;
using BrunoCPF.Modifiable.Common.Properties;
using BrunoCPF.Modifiable.Core;
using NUnit.Framework;
using R3;

namespace BrunoCPF.Modifiable.Tests
{
    public class CustomOperationsTests
    {
        // Above the threshold, only half of the excess counts.
        private static double SoftCap(double value, double threshold) =>
            value <= threshold ? value : threshold + (value - threshold) / 2;

        private static CustomOperations Registry() => new CustomOperations()
            .AddModifierOperation("soft-cap", SoftCap)
            .AddModifierOperation("square", static (value, _) => value * value)
            .AddDeltaOperation("percent-of-delta", static (delta, percent) => delta * percent / 100)
            .AddDeltaOperation("endure", static (delta, floor) => Math.Max(delta, floor));

        [Test]
        public void CustomModifiers_RunTheRegisteredOperation()
        {
            Modifier<string>[] modifiers =
            {
                new Modifier<string>(ModifierOperation.Add, 300, Source: "sword"),
                new Modifier<string>(ModifierOperation.Custom, 999, Priority: 1, CustomName: "soft-cap", Source: "cap"),
            };

            // 900 + 300 = 1200 → 999 + 201 / 2 = 1099.5 → 1099 (toward zero)
            Assert.AreEqual(1099, ModifierStack.Evaluate(900, modifiers, operations: Registry()));
        }

        [Test]
        public void CustomModifiers_AppearInBreakdownsWithTheirName()
        {
            Modifier<string>[] modifiers = { new Modifier<string>(ModifierOperation.Custom, 0, CustomName: "square", Source: "sq") };

            ModifierBreakdown<int, string> breakdown = ModifierStack.Explain(7, modifiers, operations: Registry());

            Assert.AreEqual(49, breakdown.Result);
            Assert.AreEqual("square", breakdown.Steps[0].Data!.CustomName);
        }

        [Test]
        public void CustomModifiers_TakePartInStackGroups()
        {
            Modifier<string>[] modifiers =
            {
                new Modifier<string>(ModifierOperation.Custom, 500, StackGroup: "cap", CustomName: "soft-cap", Source: "cap-a"),
                new Modifier<string>(ModifierOperation.Custom, 800, StackGroup: "cap", CustomName: "soft-cap", Source: "cap-b"),
            };

            int result = ModifierStack.Evaluate(1000, modifiers, stacking: _ => StackPolicy.HighestOnly, operations: Registry());

            Assert.AreEqual(900, result);
        }

        [Test]
        public void CustomModifiers_WithoutARegistryThrow()
        {
            Modifier<string>[] modifiers = { new Modifier<string>(ModifierOperation.Custom, 999, CustomName: "soft-cap", Source: "cap") };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ModifierStack.Evaluate(10, modifiers))!;
            StringAssert.Contains("soft-cap", error.Message);
        }

        [Test]
        public void CustomModifiers_WithAnUnknownNameThrow()
        {
            Modifier<string>[] modifiers = { new Modifier<string>(ModifierOperation.Custom, 999, CustomName: "hard-cap", Source: "cap") };

            Assert.Throws<InvalidOperationException>(() => ModifierStack.Evaluate(10, modifiers, operations: Registry()));
        }

        [Test]
        public void CustomOperations_MustReturnFiniteResults()
        {
            var registry = new CustomOperations().AddModifierOperation("broken", static (_, _) => double.NaN);
            Modifier<string>[] modifiers = { new Modifier<string>(ModifierOperation.Custom, 0, CustomName: "broken", Source: "x") };

            Assert.Throws<InvalidOperationException>(() => ModifierStack.Evaluate(1.0, modifiers, operations: registry));
        }

        [Test]
        public void ModifierMath_RejectsCustomWithoutAModifier()
        {
            Assert.Throws<ArgumentException>(() => ModifierMath.Apply(1, ModifierOperation.Custom, 2, ValueMath.GetScalableValueMath<int>()!));
        }

        [Test]
        public void CustomDeltaFilters_RunTheRegisteredOperationAndReportFiring()
        {
            DeltaFilter<string>[] filters =
            {
                new DeltaFilter<string>(DeltaOperation.Custom, 25, DeltaDirection.Decrease, CustomName: "percent-of-delta", Source: "ward"),
            };

            DeltaResult<int, string> hit = DeltaPipeline.Apply(100, -40, filters, operations: Registry());
            DeltaResult<int, string> heal = DeltaPipeline.Apply(100, 40, filters, operations: Registry());

            Assert.AreEqual(-10, hit.Filtered);
            CollectionAssert.AreEqual(new[] { "ward" }, hit.FiredFilters.Select(step => step.Source).ToArray());
            Assert.AreEqual(40, heal.Filtered);
            Assert.IsEmpty(heal.FiredFilters);
        }

        [Test]
        public void CustomDeltaFilters_WithoutARegistryThrow()
        {
            DeltaFilter<string>[] filters = { new DeltaFilter<string>(DeltaOperation.Custom, -99, CustomName: "endure", Source: "endure") };

            Assert.Throws<InvalidOperationException>(() => DeltaPipeline.Apply(100, -500, filters));
        }

        [Test]
        public void Registry_RejectsBlankAndDuplicateNames()
        {
            var registry = new CustomOperations().AddModifierOperation("x", static (v, _) => v);

            Assert.Throws<ArgumentException>(() => registry.AddModifierOperation("x", static (v, _) => v));
            Assert.Throws<ArgumentException>(() => registry.AddModifierOperation(" ", static (v, _) => v));
            Assert.Throws<ArgumentNullException>(() => registry.AddDeltaOperation("y", null!));

            // Modifier and delta operations live in separate namespaces.
            Assert.DoesNotThrow(() => registry.AddDeltaOperation("x", static (d, _) => d));
            Assert.IsTrue(registry.HasModifierOperation("x"));
            Assert.IsTrue(registry.HasDeltaOperation("x"));
            Assert.IsFalse(registry.HasModifierOperation("X"));
        }

        [Test]
        public void CustomName_IsRequiredForCustomAndRejectedOtherwise()
        {
            Assert.Throws<ArgumentException>(() => _ = new Modifier<string>(ModifierOperation.Custom, 1, Source: "a"));
            Assert.Throws<ArgumentException>(() => _ = new Modifier<string>(ModifierOperation.Custom, 1, CustomName: " ", Source: "a"));
            Assert.Throws<ArgumentException>(() => _ = new Modifier<string>(ModifierOperation.Add, 1, CustomName: "soft-cap", Source: "a"));
            Assert.Throws<ArgumentException>(() => _ = new DeltaFilter<string>(DeltaOperation.Custom, Source: "a"));
            Assert.Throws<ArgumentException>(() => _ = new DeltaFilter<string>(DeltaOperation.Block, CustomName: "endure", Source: "a"));
        }

        [Test]
        public void CustomName_IsPartOfEquality()
        {
            Assert.AreNotEqual(
                new Modifier<string>(ModifierOperation.Custom, 1, CustomName: "soft-cap", Source: "a"),
                new Modifier<string>(ModifierOperation.Custom, 1, CustomName: "square", Source: "a"));
        }

        [Test]
        public void Property_UsesItsRegistryForCustomEntries()
        {
            using ModifiableProperty<int, object, string> hp = new(100, new ValueBounds<int>(0, 2000)) { Operations = Registry() };
            var results = new List<DeltaResult<int, string>>();
            using IDisposable subscription = hp.AppliedDeltas.Subscribe(results.Add);

            hp.PushModifier(new Modifier<string>(ModifierOperation.Custom, 999, CustomName: "soft-cap", Source: "cap"));
            hp.PushFilter(new DeltaFilter<string>(DeltaOperation.Custom, -99, CustomName: "endure", Source: "endure"));

            hp.AddDelta(+1000);
            Assert.AreEqual(1100, hp.Base.CurrentValue);
            Assert.AreEqual(1049, hp.CurrentValue); // 999 + 101 / 2 = 1049.5 → 1049

            hp.AddDelta(-5000);
            Assert.AreEqual(1001, hp.Base.CurrentValue);
            CollectionAssert.AreEqual(new[] { "endure" }, results[^1].FiredFilters.Select(step => step.Source).ToArray());
        }

        [Test]
        public void Property_RejectsCustomEntriesItCannotResolve()
        {
            using ModifiableProperty<int, object, string> hp = new(100, new ValueBounds<int>(0, 2000));

            Assert.Throws<InvalidOperationException>(() =>
                hp.PushModifier(new Modifier<string>(ModifierOperation.Custom, 999, CustomName: "soft-cap", Source: "cap")));

            hp.Operations = new CustomOperations();
            Assert.Throws<InvalidOperationException>(() =>
                hp.PushFilter(new DeltaFilter<string>(DeltaOperation.Custom, -99, CustomName: "endure", Source: "endure")));
        }

        [Test]
        public void Property_RejectsARegistryThatDropsAnOperationInUse()
        {
            using ModifiableProperty<int, object, string> hp = new(100, new ValueBounds<int>(0, 2000)) { Operations = Registry() };
            hp.PushModifier(new Modifier<string>(ModifierOperation.Custom, 0, CustomName: "square", Source: "sq"));

            Assert.Throws<InvalidOperationException>(() => hp.Operations = new CustomOperations());
            Assert.Throws<InvalidOperationException>(() => hp.Operations = null);
            Assert.AreEqual(10000, hp.CurrentValue);
        }

        [Test]
        public void Property_ReevaluatesWhenTheRegistryChanges()
        {
            using ModifiableProperty<double, object, string> value = new(3.0) { Operations = new CustomOperations().AddModifierOperation("f", static (v, _) => v * 2) };
            value.PushModifier(new Modifier<string>(ModifierOperation.Custom, 0, CustomName: "f", Source: "f"));
            Assert.AreEqual(6.0, value.CurrentValue);

            value.Operations = new CustomOperations().AddModifierOperation("f", static (v, _) => v * 10);

            Assert.AreEqual(30.0, value.CurrentValue);
        }
    }
}
