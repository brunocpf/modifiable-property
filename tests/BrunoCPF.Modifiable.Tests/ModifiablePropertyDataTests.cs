using System;
using System.Collections.Generic;
using BrunoCPF.Modifiable.Common.Properties;
using BrunoCPF.Modifiable.Core;
using NUnit.Framework;
using R3;

namespace BrunoCPF.Modifiable.Tests
{
    public class ModifiablePropertyDataTests
    {
        [Test]
        public void DataModifiers_ChangeTheEffectiveValue()
        {
            using ModifiableProperty<int, object> strength = new(40, new ValueBounds<int>(0, 999));

            using (strength.PushModifier(new Modifier("sword", ModifierOperation.Add, 10)))
            {
                Assert.AreEqual(50, strength.CurrentValue);
                Assert.AreEqual(40, strength.Base.CurrentValue);
            }

            Assert.AreEqual(40, strength.CurrentValue);
        }

        [Test]
        public void DataAndFunctionModifiers_ShareOnePriorityOrder()
        {
            using ModifiableProperty<int, object> strength = new(10, new ValueBounds<int>(0, 999));

            strength.AddOrUpdateModifier(new Modifier("double", ModifierOperation.Multiply, 2, Priority: 1));
            strength.AddOrUpdateModifier("plus-five", value => value + 5);

            // (10 + 5) * 2
            Assert.AreEqual(30, strength.CurrentValue);
        }

        [Test]
        public void DataAndFunctionModifiers_ShareOneIdSpace()
        {
            using ModifiableProperty<int, object> strength = new(10, new ValueBounds<int>(0, 999));
            var functions = new List<int>();
            var data = new List<int>();
            using IDisposable functionSubscription = strength.Modifiers.Subscribe(list => functions.Add(list.Count));
            using IDisposable dataSubscription = strength.DataModifiers.Subscribe(list => data.Add(list.Count));

            strength.AddOrUpdateModifier("buff", value => value + 1);
            strength.AddOrUpdateModifier(new Modifier("buff", ModifierOperation.Add, 100));

            Assert.AreEqual(110, strength.CurrentValue);
            Assert.AreEqual(0, functions[^1]);
            Assert.AreEqual(1, data[^1]);
        }

        [Test]
        public void Stacking_ResolvesStackGroupsAndReevaluatesWhenChanged()
        {
            using ModifiableProperty<int, object> attack = new(100, new ValueBounds<int>(0, 9999));
            attack.AddOrUpdateModifier(new Modifier("boost-a", ModifierOperation.Multiply, 1.5, StackGroup: "boost"));
            attack.AddOrUpdateModifier(new Modifier("boost-b", ModifierOperation.Multiply, 2, StackGroup: "boost"));

            Assert.AreEqual(300, attack.CurrentValue);

            attack.Stacking = _ => StackPolicy.HighestOnly;

            Assert.AreEqual(200, attack.CurrentValue);
        }

        [Test]
        public void Explain_IncludesDataAndFunctionModifiers()
        {
            using ModifiableProperty<int, object> attack = new(40, new ValueBounds<int>(0, 9999));
            attack.AddOrUpdateModifier("sword", value => value + 10);
            attack.AddOrUpdateModifier(new Modifier("boost", ModifierOperation.Multiply, 1.2, Priority: 1));

            ModifierBreakdown<int> breakdown = attack.Explain();

            Assert.AreEqual(40, breakdown.Base);
            Assert.AreEqual(60, breakdown.Result);
            Assert.AreEqual(attack.CurrentValue, breakdown.Result);
            Assert.IsNull(breakdown.Steps[0].Data);
            Assert.IsNotNull(breakdown.Steps[1].Data);
        }

        [Test]
        public void DataFilters_TransformDeltasAndAreReportedWhenTheyFire()
        {
            using ModifiableProperty<int, object> hp = new(100, new ValueBounds<int>(0, 100));
            var results = new List<DeltaResult<int>>();
            using IDisposable subscription = hp.AppliedDeltas.Subscribe(results.Add);

            using (hp.PushFilter(new DeltaFilter("guard", DeltaOperation.Scale, 0.5, DeltaDirection.Decrease)))
            {
                hp.AddDelta(-30);
                hp.AddDelta(+10);
            }

            hp.AddDelta(-30);

            // 100 - 15 (guarded) + 10 - 30
            Assert.AreEqual(65, hp.CurrentValue);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual((-30, -15, -15), (results[0].Requested, results[0].Filtered, results[0].Applied));
            CollectionAssert.AreEqual(new[] { "guard" }, results[0].FiredFilters);
            Assert.AreEqual((10, 10, 10), (results[1].Requested, results[1].Filtered, results[1].Applied));
            Assert.IsEmpty(results[1].FiredFilters);
            Assert.IsEmpty(results[2].FiredFilters);
        }

        [Test]
        public void AppliedDeltas_ReportFunctionFiltersThatChangeTheDelta()
        {
            using ModifiableProperty<int, string> hp = new(100, new ValueBounds<int>(0, 100));
            DeltaResult<int>? last = null;
            ValueDelta<int, string>? processed = null;
            using IDisposable results = hp.AppliedDeltas.Subscribe(result => last = result);
            using IDisposable deltas = hp.ProcessedDeltas.Subscribe(delta => processed = delta);

            hp.AddOrUpdateFilter("endure", delta => delta with { Delta = Math.Max(delta.Delta, -99) });
            hp.AddDelta(-500, "boss");

            Assert.AreEqual(1, hp.CurrentValue);
            CollectionAssert.AreEqual(new[] { "endure" }, last!.FiredFilters);
            Assert.AreEqual("boss", processed!.Context);
            Assert.AreEqual(-99, processed.Delta);
        }

        [Test]
        public void DataFiltersAndFunctionFilters_ShareOnePriorityOrder()
        {
            using ModifiableProperty<int, object> exp = new(0, new ValueBounds<int>(0, int.MaxValue));

            exp.AddOrUpdateFilter(new DeltaFilter("double", DeltaOperation.Scale, 2, Priority: 1));
            exp.AddOrUpdateFilter("plus-one", delta => delta with { Delta = delta.Delta + 1 });

            exp.AddDelta(10);

            Assert.AreEqual(22, exp.CurrentValue);
        }

        [Test]
        public void ClearFilters_RemovesDataFiltersToo()
        {
            using ModifiableProperty<int, object> exp = new(0, new ValueBounds<int>(0, int.MaxValue));
            exp.AddOrUpdateFilter(new DeltaFilter("block", DeltaOperation.Block));

            exp.ClearFilters();
            exp.AddDelta(10);

            Assert.AreEqual(10, exp.CurrentValue);
        }

        [Test]
        public void DataEntries_RequireScalableMath()
        {
            using ModifiableProperty<TimeSpan, object> duration = new(TimeSpan.Zero, new ValueBounds<TimeSpan>(TimeSpan.Zero, TimeSpan.MaxValue));

            Assert.Throws<InvalidOperationException>(() => duration.AddOrUpdateModifier(new Modifier("a", ModifierOperation.Add, 1)));
            Assert.Throws<InvalidOperationException>(() => duration.AddOrUpdateFilter(new DeltaFilter("a", DeltaOperation.Block)));
        }

        [Test]
        public void Mutations_AfterDisposeThrow()
        {
            ModifiableProperty<int, object> hp = new(100, new ValueBounds<int>(0, 100));
            hp.Dispose();

            Assert.Throws<ObjectDisposedException>(() => hp.AddDelta(-1));
            Assert.Throws<ObjectDisposedException>(() => hp.AddOrUpdateModifier(new Modifier("a", ModifierOperation.Add, 1)));
            Assert.Throws<ObjectDisposedException>(() => hp.AddOrUpdateFilter(new DeltaFilter("a", DeltaOperation.Block)));
            Assert.DoesNotThrow(() => hp.RemoveModifier("a"));
            Assert.DoesNotThrow(() => hp.RemoveFilter("a"));
        }
    }
}
