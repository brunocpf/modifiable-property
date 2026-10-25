using System;
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
            Modifier[] modifiers =
            {
                new Modifier("sword", ModifierOperation.Add, 300),
                new Modifier("cap", ModifierOperation.Custom, 999, Priority: 1, CustomName: "soft-cap"),
            };

            // 900 + 300 = 1200 → 999 + 201 / 2 = 1099.5 → 1099 (toward zero)
            Assert.AreEqual(1099, ModifierStack.Evaluate(900, modifiers, operations: Registry()));
        }

        [Test]
        public void CustomModifiers_AppearInBreakdownsWithTheirName()
        {
            Modifier[] modifiers = { new Modifier("sq", ModifierOperation.Custom, 0, CustomName: "square") };

            ModifierBreakdown<int> breakdown = ModifierStack.Explain(7, modifiers, operations: Registry());

            Assert.AreEqual(49, breakdown.Result);
            Assert.AreEqual("square", breakdown.Steps[0].Data!.CustomName);
        }

        [Test]
        public void CustomModifiers_TakePartInStackGroups()
        {
            Modifier[] modifiers =
            {
                new Modifier("cap-a", ModifierOperation.Custom, 500, StackGroup: "cap", CustomName: "soft-cap"),
                new Modifier("cap-b", ModifierOperation.Custom, 800, StackGroup: "cap", CustomName: "soft-cap"),
            };

            int result = ModifierStack.Evaluate(1000, modifiers, stacking: _ => StackPolicy.HighestOnly, operations: Registry());

            Assert.AreEqual(900, result);
        }

        [Test]
        public void CustomModifiers_WithoutARegistryThrow()
        {
            Modifier[] modifiers = { new Modifier("cap", ModifierOperation.Custom, 999, CustomName: "soft-cap") };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ModifierStack.Evaluate(10, modifiers))!;
            StringAssert.Contains("soft-cap", error.Message);
        }

        [Test]
        public void CustomModifiers_WithAnUnknownNameThrow()
        {
            Modifier[] modifiers = { new Modifier("cap", ModifierOperation.Custom, 999, CustomName: "hard-cap") };

            Assert.Throws<InvalidOperationException>(() => ModifierStack.Evaluate(10, modifiers, operations: Registry()));
        }

        [Test]
        public void CustomOperations_MustReturnFiniteResults()
        {
            var registry = new CustomOperations().AddModifierOperation("broken", static (_, _) => double.NaN);
            Modifier[] modifiers = { new Modifier("x", ModifierOperation.Custom, 0, CustomName: "broken") };

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
            DeltaFilter[] filters =
            {
                new DeltaFilter("ward", DeltaOperation.Custom, 25, DeltaDirection.Decrease, CustomName: "percent-of-delta"),
            };

            DeltaResult<int> hit = DeltaPipeline.Apply(100, -40, filters, operations: Registry());
            DeltaResult<int> heal = DeltaPipeline.Apply(100, 40, filters, operations: Registry());

            Assert.AreEqual(-10, hit.Filtered);
            CollectionAssert.AreEqual(new[] { "ward" }, hit.FiredFilters);
            Assert.AreEqual(40, heal.Filtered);
            Assert.IsEmpty(heal.FiredFilters);
        }

        [Test]
        public void CustomDeltaFilters_WithoutARegistryThrow()
        {
            DeltaFilter[] filters = { new DeltaFilter("endure", DeltaOperation.Custom, -99, CustomName: "endure") };

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
            Assert.Throws<ArgumentException>(() => _ = new Modifier("a", ModifierOperation.Custom, 1));
            Assert.Throws<ArgumentException>(() => _ = new Modifier("a", ModifierOperation.Custom, 1, CustomName: " "));
            Assert.Throws<ArgumentException>(() => _ = new Modifier("a", ModifierOperation.Add, 1, CustomName: "soft-cap"));
            Assert.Throws<ArgumentException>(() => _ = new DeltaFilter("a", DeltaOperation.Custom));
            Assert.Throws<ArgumentException>(() => _ = new DeltaFilter("a", DeltaOperation.Block, CustomName: "endure"));
        }

        [Test]
        public void CustomName_IsPartOfEquality()
        {
            Assert.AreNotEqual(
                new Modifier("a", ModifierOperation.Custom, 1, CustomName: "soft-cap"),
                new Modifier("a", ModifierOperation.Custom, 1, CustomName: "square"));
        }

        [Test]
        public void Property_UsesItsRegistryForCustomEntries()
        {
            using ModifiableProperty<int, object> hp = new(100, new ValueBounds<int>(0, 2000)) { Operations = Registry() };
            var results = new List<DeltaResult<int>>();
            using IDisposable subscription = hp.AppliedDeltas.Subscribe(results.Add);

            hp.AddOrUpdateModifier(new Modifier("cap", ModifierOperation.Custom, 999, CustomName: "soft-cap"));
            hp.AddOrUpdateFilter(new DeltaFilter("endure", DeltaOperation.Custom, -99, CustomName: "endure"));

            hp.AddDelta(+1000);
            Assert.AreEqual(1100, hp.Base.CurrentValue);
            Assert.AreEqual(1049, hp.CurrentValue); // 999 + 101 / 2 = 1049.5 → 1049

            hp.AddDelta(-5000);
            Assert.AreEqual(1001, hp.Base.CurrentValue);
            CollectionAssert.AreEqual(new[] { "endure" }, results[^1].FiredFilters);
        }

        [Test]
        public void Property_RejectsCustomEntriesItCannotResolve()
        {
            using ModifiableProperty<int, object> hp = new(100, new ValueBounds<int>(0, 2000));

            Assert.Throws<InvalidOperationException>(() =>
                hp.AddOrUpdateModifier(new Modifier("cap", ModifierOperation.Custom, 999, CustomName: "soft-cap")));

            hp.Operations = new CustomOperations();
            Assert.Throws<InvalidOperationException>(() =>
                hp.AddOrUpdateFilter(new DeltaFilter("endure", DeltaOperation.Custom, -99, CustomName: "endure")));
        }

        [Test]
        public void Property_RejectsARegistryThatDropsAnOperationInUse()
        {
            using ModifiableProperty<int, object> hp = new(100, new ValueBounds<int>(0, 2000)) { Operations = Registry() };
            hp.AddOrUpdateModifier(new Modifier("sq", ModifierOperation.Custom, 0, CustomName: "square"));

            Assert.Throws<InvalidOperationException>(() => hp.Operations = new CustomOperations());
            Assert.Throws<InvalidOperationException>(() => hp.Operations = null);
            Assert.AreEqual(10000, hp.CurrentValue);
        }

        [Test]
        public void Property_ReevaluatesWhenTheRegistryChanges()
        {
            using ModifiableProperty<double, object> value = new(3.0) { Operations = new CustomOperations().AddModifierOperation("f", static (v, _) => v * 2) };
            value.AddOrUpdateModifier(new Modifier("f", ModifierOperation.Custom, 0, CustomName: "f"));
            Assert.AreEqual(6.0, value.CurrentValue);

            value.Operations = new CustomOperations().AddModifierOperation("f", static (v, _) => v * 10);

            Assert.AreEqual(30.0, value.CurrentValue);
        }
    }
}
