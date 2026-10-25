using System;
using System.Collections.Generic;
using System.Linq;
using BrunoCPF.Modifiable.Common.Properties;
using BrunoCPF.Modifiable.Core;
using NUnit.Framework;
using R3;

namespace BrunoCPF.Modifiable.Tests
{
    public class ModifiablePropertySemanticsTests
    {
        [Test]
        public void Notifications_FollowTheDocumentedOrder()
        {
            using var hp = new ModifiableProperty<int, object, string>(10);
            hp.PushModifier(v => v * 2);
            var log = new List<string>();

            using IDisposable baseSub = hp.Base.Subscribe(v => log.Add($"base {v} (current {hp.CurrentValue})"));
            using IDisposable effectiveSub = hp.Effective.Subscribe(v => log.Add($"effective {v} (base {hp.Base.CurrentValue})"));
            using IDisposable processedSub = hp.ProcessedDeltas.Subscribe(d => log.Add($"processed {d.Delta}"));
            using IDisposable appliedSub = hp.AppliedDeltas.Subscribe(r => log.Add($"applied {r.Before}->{r.After}"));
            log.Clear();

            hp.AddDelta(5);

            CollectionAssert.AreEqual(
                new[] { "base 15 (current 30)", "effective 30 (base 15)", "processed 5", "applied 10->15" },
                log);
        }

        [Test]
        public void DeltasFromSubscribers_AreQueuedSoAppliedDeltasReadsAsALog()
        {
            using var hp = new ModifiableProperty<int, object, string>(0);
            var log = new List<string>();
            using IDisposable applied = hp.AppliedDeltas.Subscribe(r => log.Add($"{r.Before}->{r.After}"));
            using IDisposable reaction = hp.Base.Subscribe(v =>
            {
                if (v == 1)
                {
                    hp.AddDelta(10);
                    Assert.AreEqual(1, hp.Base.CurrentValue, "queued, not applied mid-notification");
                }
            });

            hp.AddDelta(1);

            CollectionAssert.AreEqual(new[] { "0->1", "1->11" }, log);
            Assert.AreEqual(11, hp.CurrentValue);
        }

        [Test]
        public void ReplayingAppliedDeltas_ReproducesTheFinalState()
        {
            using var hp = new ModifiableProperty<int, object, string>(0, new ValueBounds<int>(0, 100));
            var applied = new List<int>();
            using IDisposable log = hp.AppliedDeltas.Subscribe(r => applied.Add(r.Applied));
            using IDisposable regen = hp.Base.Subscribe(v =>
            {
                if (v is > 0 and < 50)
                {
                    hp.AddDelta(20);
                }
            });

            hp.AddDelta(5);

            using var replay = new ModifiableProperty<int, object, string>(0, new ValueBounds<int>(0, 100));
            foreach (int delta in applied)
            {
                replay.AddDelta(delta);
            }

            Assert.AreEqual(hp.Base.CurrentValue, replay.Base.CurrentValue);
        }

        [Test]
        public void QueuedSetBaseValue_IsComputedWhenApplied()
        {
            using var hp = new ModifiableProperty<int, object, string>(0);
            using IDisposable reaction = hp.Base.Subscribe(v =>
            {
                if (v == 1)
                {
                    hp.AddDelta(5);
                    hp.SetBaseValue(100);
                }
            });

            hp.AddDelta(1);

            Assert.AreEqual(100, hp.CurrentValue);
        }

        [Test]
        public void SubscriberPushingAModifier_EndsWithTheModifiedValue()
        {
            using var hp = new ModifiableProperty<int, object, string>(10);
            using IDisposable reaction = hp.Base.Subscribe(v =>
            {
                if (v == 15)
                {
                    hp.PushModifier(x => x + 1000);
                }
            });

            hp.AddDelta(5);

            Assert.AreEqual(1015, hp.CurrentValue);
            Assert.AreEqual(1015, hp.Effective.CurrentValue);
        }

        [Test]
        public void FailingCustomOperation_ThrowsAndLeavesThePropertyUnchanged()
        {
            var operations = new CustomOperations().AddModifierOperation("nan-above-100", (v, _) => v > 100 ? double.NaN : v);
            using var value = new ModifiableProperty<double, object, string>(10.0) { Operations = operations };
            value.PushModifier(new Modifier<string>(ModifierOperation.Custom, 0, CustomName: "nan-above-100"));
            var applied = new List<DeltaResult<double, string>>();
            using IDisposable sub = value.AppliedDeltas.Subscribe(applied.Add);

            Assert.Throws<InvalidOperationException>(() => value.AddDelta(500));

            Assert.AreEqual(10.0, value.Base.CurrentValue);
            Assert.AreEqual(10.0, value.CurrentValue);
            Assert.IsEmpty(applied);

            value.AddDelta(5);
            Assert.AreEqual(15.0, value.CurrentValue);
        }

        [Test]
        public void FailingFunctionModifier_IsNotInstalled()
        {
            using var hp = new ModifiableProperty<int, object, string>(10);

            Assert.Throws<InvalidOperationException>(() => hp.PushModifier(_ => throw new InvalidOperationException("boom")));

            Assert.AreEqual(10, hp.CurrentValue);
            hp.AddDelta(1);
            Assert.AreEqual(11, hp.CurrentValue);
        }

        [Test]
        public void FailingFunctionFilter_ThrowsAndLeavesTheBaseUnchanged()
        {
            using var hp = new ModifiableProperty<int, object, string>(10);
            hp.PushFilter(d => d.Delta > 100 ? throw new InvalidOperationException("too big") : d);

            Assert.Throws<InvalidOperationException>(() => hp.AddDelta(500));

            Assert.AreEqual(10, hp.Base.CurrentValue);
        }

        [Test]
        public void QueuedDeltaThatThrows_ReachesTheOuterCallerAndDropsTheRest()
        {
            using var hp = new ModifiableProperty<int, object, string>(0);
            hp.PushFilter(d => d.Delta == 13 ? throw new InvalidOperationException("unlucky") : d);
            using IDisposable reaction = hp.Base.Subscribe(v =>
            {
                if (v == 1)
                {
                    hp.AddDelta(13);
                    hp.AddDelta(100);
                }
            });

            Assert.Throws<InvalidOperationException>(() => hp.AddDelta(1));

            Assert.AreEqual(1, hp.CurrentValue);
            hp.AddDelta(2);
            Assert.AreEqual(3, hp.CurrentValue);
        }

        [Test]
        public void InvalidStacking_IsRejectedAndTheOldPolicyStays()
        {
            using var attack = new ModifiableProperty<int, object, string>(100);
            attack.PushModifier(new Modifier<string>(ModifierOperation.Multiply, 2, StackGroup: "mixed"));
            attack.PushModifier(new Modifier<string>(ModifierOperation.Add, 5, StackGroup: "mixed"));

            Assert.Throws<InvalidOperationException>(() => attack.Stacking = _ => StackPolicy.HighestOnly);

            Assert.IsNull(attack.Stacking);
            Assert.AreEqual(205, attack.CurrentValue);
        }

        [Test]
        public void ChangingStackingOrOperations_DoesNotReemitModifiers()
        {
            using var attack = new ModifiableProperty<int, object, string>(100);
            attack.PushModifier(new Modifier<string>(ModifierOperation.Multiply, 2, StackGroup: "boost"));
            int emissions = 0;
            using IDisposable sub = attack.Modifiers.Subscribe(_ => emissions++);
            emissions = 0;

            attack.Stacking = _ => StackPolicy.HighestOnly;
            attack.Operations = new CustomOperations();

            Assert.AreEqual(0, emissions);
        }

        [Test]
        public void RemoveBySource_NullAndDefaultMatchSourcelessEntries()
        {
            using var hp = new ModifiableProperty<int, object, string>(0);
            hp.PushModifier(v => v + 1);
            hp.PushModifier(v => v + 10, source: "ring");
            hp.PushFilter(d => d with { Delta = d.Delta * 2 });

            Assert.AreEqual(1, hp.RemoveModifiers(null));
            Assert.AreEqual(1, hp.RemoveFilters(null));
            Assert.AreEqual(10, hp.CurrentValue);

            using var counter = new ModifiableProperty<int, object, int>(0);
            counter.PushModifier(v => v + 1);              // source: default(int) == 0
            counter.PushModifier(v => v + 10, source: 7);

            Assert.AreEqual(1, counter.RemoveModifiers(0));
            Assert.AreEqual(10, counter.CurrentValue);
        }

        [Test]
        public void TwoParameterProperty_ConstructsWithCustomMathUnambiguously()
        {
            using var value = new ModifiableProperty<int, object>(10, valueMath: new Common.Math.IntValueMath(Common.Math.RoundingMode.Floor));
            value.PushModifier(new Modifier<object>(ModifierOperation.Multiply, -1.5));

            Assert.AreEqual(-15, value.CurrentValue);
        }
    }
}
