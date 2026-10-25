using System;
using System.Collections.Generic;
using System.Linq;
using BrunoCPF.Modifiable.Common.Properties;
using BrunoCPF.Modifiable.Core;
using NUnit.Framework;
using R3;

namespace BrunoCPF.Modifiable.Tests
{
    public class ModifiablePropertyDataTests
    {
        private static ModifiableProperty<int, object, string> NewInt(int initial, int max = 9999) =>
            new(initial, new ValueBounds<int>(0, max));

        [Test]
        public void DataModifiers_ChangeTheEffectiveValue()
        {
            using var strength = NewInt(40, 999);

            using (strength.PushModifier(new Modifier<string>(ModifierOperation.Add, 10, Source: "sword")))
            {
                Assert.AreEqual(50, strength.CurrentValue);
                Assert.AreEqual(40, strength.Base.CurrentValue);
            }

            Assert.AreEqual(40, strength.CurrentValue);
        }

        [Test]
        public void DataAndFunctionModifiers_ShareOnePriorityOrder()
        {
            using var strength = NewInt(10, 999);

            strength.PushModifier(new Modifier<string>(ModifierOperation.Multiply, 2, Priority: 1));
            strength.PushModifier(value => value + 5);

            // (10 + 5) * 2
            Assert.AreEqual(30, strength.CurrentValue);
        }

        [Test]
        public void RemoveModifiers_RemovesDataAndFunctionEntriesFromTheSource()
        {
            using var strength = NewInt(10, 999);

            strength.PushModifier(new Modifier<string>(ModifierOperation.Add, 100, Source: "buff"));
            strength.PushModifier(value => value + 1, source: "buff");
            strength.PushModifier(new Modifier<string>(ModifierOperation.Add, 3, Source: "ring"));

            Assert.AreEqual(2, strength.RemoveModifiers("buff"));
            Assert.AreEqual(13, strength.CurrentValue);
        }

        [Test]
        public void Modifiers_StreamExposesDataEntries()
        {
            using var strength = NewInt(10, 999);
            IReadOnlyList<ModifierEntry<int, string>>? latest = null;
            using IDisposable subscription = strength.Modifiers.Subscribe(list => latest = list);
            var sword = new Modifier<string>(ModifierOperation.Add, 10, Source: "sword");

            strength.PushModifier(sword);

            Assert.AreSame(sword, latest!.Single().Data);
            Assert.AreEqual("sword", latest![0].Source);
            Assert.IsNull(latest[0].Function);
        }

        [Test]
        public void Stacking_ResolvesStackGroupsAndReevaluatesWhenChanged()
        {
            using var attack = NewInt(100);
            attack.PushModifier(new Modifier<string>(ModifierOperation.Multiply, 1.5, StackGroup: "boost", Source: "ring"));
            attack.PushModifier(new Modifier<string>(ModifierOperation.Multiply, 2, StackGroup: "boost", Source: "skill"));

            Assert.AreEqual(300, attack.CurrentValue);

            attack.Stacking = _ => StackPolicy.HighestOnly;

            Assert.AreEqual(200, attack.CurrentValue);
        }

        [Test]
        public void Explain_ReportsSourcesForDataAndFunctionModifiers()
        {
            using var attack = NewInt(40);
            attack.PushModifier(value => value + 10, source: "sword");
            attack.PushModifier(new Modifier<string>(ModifierOperation.Multiply, 1.2, Priority: 1, Source: "boost"));

            ModifierBreakdown<int, string> breakdown = attack.Explain();

            Assert.AreEqual(40, breakdown.Base);
            Assert.AreEqual(60, breakdown.Result);
            Assert.AreEqual(attack.CurrentValue, breakdown.Result);
            CollectionAssert.AreEqual(new[] { "sword", "boost" }, breakdown.Steps.Select(step => step.Source).ToArray());
            Assert.IsNull(breakdown.Steps[0].Data);
            Assert.IsNotNull(breakdown.Steps[1].Data);
        }

        [Test]
        public void DataFilters_TransformDeltasAndAreReportedWhenTheyFire()
        {
            using var hp = NewInt(100, 100);
            var results = new List<DeltaResult<int, string>>();
            using IDisposable subscription = hp.AppliedDeltas.Subscribe(results.Add);

            using (hp.PushFilter(new DeltaFilter<string>(DeltaOperation.Scale, 0.5, DeltaDirection.Decrease, Source: "guard")))
            {
                hp.AddDelta(-30);
                hp.AddDelta(+10);
            }

            hp.AddDelta(-30);

            // 100 - 15 (guarded) + 10 - 30
            Assert.AreEqual(65, hp.CurrentValue);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual((-30, -15, -15), (results[0].Requested, results[0].Filtered, results[0].Applied));
            CollectionAssert.AreEqual(new[] { "guard" }, results[0].FiredFilters.Select(step => step.Source).ToArray());
            Assert.AreEqual((10, 10, 10), (results[1].Requested, results[1].Filtered, results[1].Applied));
            Assert.IsEmpty(results[1].FiredFilters);
            Assert.IsEmpty(results[2].FiredFilters);
        }

        [Test]
        public void AppliedDeltas_ReportFunctionFiltersThatChangeTheDelta()
        {
            using var hp = new ModifiableProperty<int, string, string>(100, new ValueBounds<int>(0, 100));
            DeltaResult<int, string>? last = null;
            ValueDelta<int, string>? processed = null;
            using IDisposable results = hp.AppliedDeltas.Subscribe(result => last = result);
            using IDisposable deltas = hp.ProcessedDeltas.Subscribe(delta => processed = delta);

            hp.PushFilter(delta => delta with { Delta = Math.Max(delta.Delta, -99) }, source: "endure");
            hp.AddDelta(-500, "boss");

            Assert.AreEqual(1, hp.CurrentValue);
            DeltaStep<int, string> step = last!.FiredFilters.Single();
            Assert.AreEqual(("endure", -500, -99), (step.Source, step.Before, step.After));
            Assert.IsNull(step.Data);
            Assert.AreEqual("boss", processed!.Context);
            Assert.AreEqual(-99, processed.Delta);
        }

        [Test]
        public void DataFiltersAndFunctionFilters_ShareOnePriorityOrder()
        {
            using var exp = new ModifiableProperty<int, object, string>(0, ValueBounds<int>.AtLeast(0));

            exp.PushFilter(new DeltaFilter<string>(DeltaOperation.Scale, 2, Priority: 1));
            exp.PushFilter(delta => delta with { Delta = delta.Delta + 1 });

            exp.AddDelta(10);

            Assert.AreEqual(22, exp.CurrentValue);
        }

        [Test]
        public void RemoveFilters_AndClearFilters_RemoveDataFiltersToo()
        {
            using var exp = new ModifiableProperty<int, object, string>(0);
            exp.PushFilter(new DeltaFilter<string>(DeltaOperation.Block, Source: "curse"));
            exp.PushFilter(new DeltaFilter<string>(DeltaOperation.Scale, 3, Source: "boost"));

            Assert.AreEqual(1, exp.RemoveFilters("curse"));
            exp.AddDelta(10);
            Assert.AreEqual(30, exp.CurrentValue);

            exp.ClearFilters();
            exp.AddDelta(10);
            Assert.AreEqual(40, exp.CurrentValue);
        }

        [Test]
        public void DataEntries_RequireScalableMath()
        {
            using var duration = new ModifiableProperty<TimeSpan, object, string>(TimeSpan.Zero);

            Assert.Throws<InvalidOperationException>(() => duration.PushModifier(new Modifier<string>(ModifierOperation.Add, 1)));
            Assert.Throws<InvalidOperationException>(() => duration.PushFilter(new DeltaFilter<string>(DeltaOperation.Block)));
        }

        [Test]
        public void Mutations_AfterDisposeThrowButRemovalDoesNot()
        {
            var hp = NewInt(100, 100);
            IDisposable handle = hp.PushModifier(new Modifier<string>(ModifierOperation.Add, 1, Source: "a"));
            hp.Dispose();

            Assert.Throws<ObjectDisposedException>(() => hp.AddDelta(-1));
            Assert.Throws<ObjectDisposedException>(() => hp.PushModifier(new Modifier<string>(ModifierOperation.Add, 1)));
            Assert.Throws<ObjectDisposedException>(() => hp.PushFilter(new DeltaFilter<string>(DeltaOperation.Block)));
            Assert.DoesNotThrow(handle.Dispose);
            Assert.AreEqual(0, hp.RemoveModifiers("a"));
            Assert.AreEqual(0, hp.RemoveFilters("a"));
        }
    }
}
