using System;
using System.Collections.Generic;
using System.Linq;
using BrunoCPF.Modifiable.Common.Properties;
using NUnit.Framework;
using R3;

namespace BrunoCPF.Modifiable.Tests
{
    public class ModifiablePropertyTests
    {
        [Test]
        public void AttackValueFlow_MatchesSampleExpectations()
        {
            using ModifiableProperty<float, object> atk = new(10f, new ValueBounds<float>(0f, int.MaxValue));

            Assert.AreEqual(10f, atk.CurrentValue);
            Assert.AreEqual(10f, atk.Base.CurrentValue);

            atk.AddDelta(+5f);
            Assert.AreEqual(15f, atk.CurrentValue);
            Assert.AreEqual(15f, atk.Base.CurrentValue);

            atk.AddDelta(-20f); // clamp to 0
            Assert.AreEqual(0f, atk.CurrentValue);
            Assert.AreEqual(0f, atk.Base.CurrentValue);

            atk.SetBaseValue(15f);
            Assert.AreEqual(15f, atk.CurrentValue);
            Assert.AreEqual(15f, atk.Base.CurrentValue);

            using (atk.PushModifier(baseValue => baseValue + 10f, source: "atk-mod"))
            {
                Assert.AreEqual(25f, atk.CurrentValue);
                Assert.AreEqual(15f, atk.Base.CurrentValue);

                atk.AddDelta(+20f);
                Assert.AreEqual(45f, atk.CurrentValue);
                Assert.AreEqual(35f, atk.Base.CurrentValue);

                using (atk.PushFilter(
                           delta => delta.Delta > 0
                               ? new ValueDelta<float, object>(delta.Delta * 1.5f, delta.Context)
                               : delta,
                           source: "atk-increase-boost"))
                {
                    Assert.AreEqual(45f, atk.CurrentValue);
                    Assert.AreEqual(35f, atk.Base.CurrentValue);

                    atk.AddDelta(+20f); // boosted to +30
                    Assert.AreEqual(75f, atk.CurrentValue);
                    Assert.AreEqual(65f, atk.Base.CurrentValue);
                }

                atk.AddDelta(+10f);
                Assert.AreEqual(85f, atk.CurrentValue);
                Assert.AreEqual(75f, atk.Base.CurrentValue);
            }

            Assert.AreEqual(75f, atk.CurrentValue);
            Assert.AreEqual(75f, atk.Base.CurrentValue);
        }

        [Test]
        public void ExpAndLevelFlow_MatchesSampleExpectations()
        {
            using ModifiableProperty<int, object> exp = new(0, new ValueBounds<int>(0, int.MaxValue));
            using ModifiableProperty<int, object> level = new(1, new ValueBounds<int>(1, 99));

            using (exp.Subscribe(toValue =>
                   {
                       if (toValue >= 100 && level.CurrentValue < 2)
                       {
                           level.SetBaseValue(2);
                       }
                       else if (toValue >= 300 && level.CurrentValue < 3)
                       {
                           level.SetBaseValue(3);
                       }
                   }))
            using (level.Base.Subscribe(toValue =>
                   {
                       if (toValue == 1 && exp.CurrentValue is < 0 or >= 100)
                       {
                           exp.SetBaseValue(0);
                       }
                       else if (toValue == 2 && exp.CurrentValue is < 100 or >= 300)
                       {
                           exp.SetBaseValue(100);
                       }
                       else if (toValue == 3 && (exp.CurrentValue < 300))
                       {
                           exp.SetBaseValue(300);
                       }
                   }))
            {
                Assert.AreEqual(0, exp.CurrentValue);
                Assert.AreEqual(1, level.CurrentValue);

                exp.AddDelta(100);
                Assert.AreEqual(100, exp.CurrentValue);
                Assert.AreEqual(2, level.CurrentValue);

                exp.AddDelta(200);
                Assert.AreEqual(300, exp.CurrentValue);
                Assert.AreEqual(3, level.CurrentValue);

                level.SetBaseValue(1);
                Assert.AreEqual(1, level.CurrentValue);
                Assert.AreEqual(0, exp.CurrentValue);

                level.SetBaseValue(3);
                Assert.AreEqual(3, level.CurrentValue);
                Assert.AreEqual(300, exp.CurrentValue);

                using (level.PushModifier(_ => 2, source: "fixed-level-2"))
                {
                    Assert.AreEqual(2, level.CurrentValue);
                    Assert.AreEqual(3, level.Base.CurrentValue);
                    Assert.AreEqual(300, exp.CurrentValue);
                }

                Assert.AreEqual(3, level.CurrentValue);
            }
        }

        [Test]
        public void LongExpDeltas_AccumulateInsteadOfReplacingTheCurrentValue()
        {
            using ModifiableProperty<long, object> exp = new(0L, new ValueBounds<long>(0L, long.MaxValue));

            exp.AddDelta(2472L);
            exp.AddDelta(67L);

            Assert.AreEqual(2539L, exp.CurrentValue);
            Assert.AreEqual(2539L, exp.Base.CurrentValue);
        }

        [Test]
        public void HpAndMaxHpFlow_MatchesSampleExpectations()
        {
            using ModifiableProperty<int, object> maxHp = new(500, new ValueBounds<int>(1, int.MaxValue));
            using ModifiableProperty<int, object> hp = new(500, new ValueBounds<int>(0, int.MaxValue));

            using var _overflow = hp.PushFilter(
                valueDelta =>
                {
                    int max = maxHp.CurrentValue;
                    int newHp = hp.CurrentValue + valueDelta.Delta;

                    if (newHp > max)
                    {
                        return new ValueDelta<int, object>(max - hp.CurrentValue, valueDelta.Context);
                    }

                    return valueDelta;
                },
                source: "hp-overflow-protection");

            Assert.AreEqual(500, hp.CurrentValue);
            Assert.AreEqual(500, maxHp.CurrentValue);

            hp.AddDelta(-100);
            Assert.AreEqual(400, hp.CurrentValue);
            Assert.AreEqual(500, maxHp.CurrentValue);

            hp.AddDelta(+10);
            Assert.AreEqual(410, hp.CurrentValue);
            Assert.AreEqual(500, maxHp.CurrentValue);

            hp.AddDelta(+200); // capped at max
            Assert.AreEqual(500, hp.CurrentValue);
            Assert.AreEqual(500, maxHp.CurrentValue);

            hp.AddDelta(-300);
            Assert.AreEqual(200, hp.CurrentValue);
            Assert.AreEqual(500, maxHp.CurrentValue);

            int cap = hp.CurrentValue;
            maxHp.PushModifier(v => Math.Min(v, cap), source: "disease_cap");
            using var damageSub = hp.ProcessedDeltas.Subscribe(delta =>
            {
                if (delta.Delta < 0)
                {
                    // Replace the cap: remove everything from the source, then install the new one.
                    cap = hp.CurrentValue;
                    maxHp.RemoveModifiers("disease_cap");
                    maxHp.PushModifier(v => Math.Min(v, cap), source: "disease_cap");
                }
            });

            Assert.AreEqual(200, maxHp.CurrentValue);

            hp.AddDelta(+50);
            Assert.AreEqual(200, hp.CurrentValue);
            Assert.AreEqual(200, maxHp.CurrentValue);

            hp.AddDelta(-100);
            Assert.AreEqual(100, hp.CurrentValue);
            Assert.AreEqual(100, maxHp.CurrentValue);

            Assert.AreEqual(1, maxHp.RemoveModifiers("disease_cap"));
            damageSub.Dispose();

            Assert.AreEqual(500, maxHp.CurrentValue);
        }

        [Test]
        public void RemoveModifiers_AfterDispose_DoesNotThrow()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            property.PushModifier(v => v + 10, source: "buff");
            property.Dispose();

            Assert.DoesNotThrow(() => property.RemoveModifiers("buff"));
            Assert.DoesNotThrow(property.ClearModifiers);
        }

        [Test]
        public void DisposingPushModifierHandle_AfterPropertyDisposed_DoesNotThrow()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            IDisposable handle = property.PushModifier(v => v + 10);
            property.Dispose();

            Assert.DoesNotThrow(handle.Dispose);
        }

        [Test]
        public void Modifiers_NeedNoIdentity_EqualEntriesBothApply()
        {
            using ModifiableProperty<int, object> property = new(10);
            Func<int, int> plusFive = v => v + 5;

            property.PushModifier(plusFive, source: "buff");
            property.PushModifier(plusFive, source: "buff");

            Assert.AreEqual(20, property.CurrentValue);
        }

        [Test]
        public void ModifierHandle_RemovesOnlyItsOwnEntry()
        {
            using ModifiableProperty<int, object> property = new(10);

            IDisposable first = property.PushModifier(v => v + 1, source: "buff");
            property.PushModifier(v => v + 100, source: "buff");

            first.Dispose();
            first.Dispose(); // idempotent

            Assert.AreEqual(110, property.CurrentValue);
        }

        [Test]
        public void RemoveModifiers_RemovesEverythingFromThatSource()
        {
            using ModifiableProperty<int, object> property = new(10);
            var tarukaja = new object();
            var sword = new object();

            property.PushModifier(v => v + 1, source: tarukaja);
            property.PushModifier(v => v + 10, source: sword);
            property.PushModifier(v => v + 100, source: tarukaja);

            Assert.AreEqual(2, property.RemoveModifiers(tarukaja));
            Assert.AreEqual(20, property.CurrentValue);
            Assert.AreEqual(0, property.RemoveModifiers(tarukaja));
        }

        [Test]
        public void RemoveFilters_RemovesEverythingFromThatSource()
        {
            using ModifiableProperty<int, object> property = new(0);

            property.PushFilter(d => d with { Delta = d.Delta * 2 }, source: "boost");
            property.PushFilter(d => d with { Delta = d.Delta + 1 }, source: "flat");
            property.PushFilter(d => d with { Delta = d.Delta * 3 }, source: "boost");

            Assert.AreEqual(2, property.RemoveFilters("boost"));
            property.AddDelta(10);

            Assert.AreEqual(11, property.CurrentValue);
        }

        [Test]
        public void ClearModifiers_RemovesEveryModifier()
        {
            using ModifiableProperty<int, object> property = new(10);
            property.PushModifier(v => v + 1);
            property.PushModifier(v => v * 3);

            property.ClearModifiers();

            Assert.AreEqual(10, property.CurrentValue);
        }

        [Test]
        public void Modifiers_StreamListsEntriesInEvaluationOrder()
        {
            using ModifiableProperty<int, object> property = new(10);
            IReadOnlyList<ModifierEntry<int, object>>? latest = null;
            using IDisposable subscription = property.Modifiers.Subscribe(list => latest = list);

            property.PushModifier(v => v * 2, priority: 1, source: "late");
            property.PushModifier(v => v + 1, source: "early");

            CollectionAssert.AreEqual(new object[] { "early", "late" }, latest!.Select(entry => entry.Source).ToArray());
            Assert.IsNotNull(latest![0].Function);
            Assert.IsNull(latest[0].Data);
        }

        // List<T>.Sort is an unstable introsort once a list exceeds 16 items, so these use enough
        // equal-priority entries to expose any reordering. Each function is order-dependent.
        private const int ManyEntries = 64;
        private const long Mod = 1_000_003;

        private static long Fold(long seed, int[] ids) => ids.Aggregate(seed, (v, id) => (v * 31 + id) % Mod);

        private static ModifiableProperty<long, object> NewLongProperty(long initial = 7) =>
            new(initial, new ValueBounds<long>(long.MinValue, long.MaxValue));

        [Test]
        public void Modifiers_WithEqualPriority_ApplyInInsertionOrder()
        {
            using var property = NewLongProperty();

            for (int i = 1; i <= ManyEntries; i++)
            {
                int id = i;
                property.PushModifier(v => (v * 31 + id) % Mod);
            }

            Assert.AreEqual(Fold(7, Enumerable.Range(1, ManyEntries).ToArray()), property.CurrentValue);
        }

        [Test]
        public void Modifiers_WithMixedPriorities_AreStableWithinEachPriority()
        {
            for (int run = 0; run < 20; run++)
            {
                using var property = NewLongProperty();
                var rng = new Random(run);
                var added = new List<(int Id, int Priority)>();

                for (int i = 1; i <= ManyEntries; i++)
                {
                    int id = i;
                    int priority = rng.Next(0, 4);
                    added.Add((id, priority));
                    property.PushModifier(v => (v * 31 + id) % Mod, priority);
                }

                int[] expectedOrder = added.OrderBy(a => a.Priority).Select(a => a.Id).ToArray();
                Assert.AreEqual(Fold(7, expectedOrder), property.CurrentValue, $"run {run}");
            }
        }

        [Test]
        public void Modifiers_RemoveAndReAdd_MovesEntryToEndOfItsPriority()
        {
            using var property = NewLongProperty();
            var order = Enumerable.Range(1, ManyEntries).ToList();
            var handles = new Dictionary<int, IDisposable>();

            foreach (int id in order)
            {
                int captured = id;
                handles[id] = property.PushModifier(v => (v * 31 + captured) % Mod);
            }

            handles[5].Dispose();
            property.PushModifier(v => (v * 31 + 5) % Mod);
            order.Remove(5);
            order.Add(5);

            Assert.AreEqual(Fold(7, order.ToArray()), property.CurrentValue);
        }

        [Test]
        public void Modifiers_ClassicAddThenMultiply_FollowsInsertionOrder()
        {
            using ModifiableProperty<float, object> property = new(10f, 0f, 999f);

            property.PushModifier(v => v + 10f);
            property.PushModifier(v => v * 2f);

            Assert.AreEqual(40f, property.CurrentValue);
        }

        [Test]
        public void Filters_WithEqualPriority_RunInInsertionOrder()
        {
            using var property = NewLongProperty(0);

            for (int i = 1; i <= ManyEntries; i++)
            {
                int id = i;
                property.PushFilter(d => new ValueDelta<long, object>((d.Delta * 31 + id) % Mod, d.Context));
            }

            property.AddDelta(5);

            Assert.AreEqual(Fold(5, Enumerable.Range(1, ManyEntries).ToArray()), property.Base.CurrentValue);
        }

        [Test]
        public void Filters_WithMixedPriorities_AreStableWithinEachPriority()
        {
            for (int run = 0; run < 20; run++)
            {
                using var property = NewLongProperty(0);
                var rng = new Random(run);
                var added = new List<(int Id, int Priority)>();

                for (int i = 1; i <= ManyEntries; i++)
                {
                    int id = i;
                    int priority = rng.Next(0, 4);
                    added.Add((id, priority));
                    property.PushFilter(d => new ValueDelta<long, object>((d.Delta * 31 + id) % Mod, d.Context), priority);
                }

                property.AddDelta(5);

                int[] expectedOrder = added.OrderBy(a => a.Priority).Select(a => a.Id).ToArray();
                Assert.AreEqual(Fold(5, expectedOrder), property.Base.CurrentValue, $"run {run}");
            }
        }

        [Test]
        public void FilterHandle_RemovesOnlyItsOwnEntry()
        {
            using ModifiableProperty<int, object> property = new(0);

            IDisposable doubling = property.PushFilter(d => d with { Delta = d.Delta * 2 }, source: "x");
            property.PushFilter(d => d with { Delta = d.Delta + 1 }, source: "x");
            doubling.Dispose();

            property.AddDelta(10);

            Assert.AreEqual(11, property.CurrentValue);
        }

        [Test]
        public void RemoveFilters_AfterDispose_DoesNotThrow()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            IDisposable handle = property.PushFilter(delta => delta, source: "noop");
            property.Dispose();

            Assert.DoesNotThrow(() => property.RemoveFilters("noop"));
            Assert.DoesNotThrow(handle.Dispose);
        }

        [Test]
        public void ClearFilters_AfterDispose_DoesNotThrow()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            property.PushFilter(delta => delta);
            property.Dispose();

            Assert.DoesNotThrow(property.ClearFilters);
        }

        [Test]
        public void TypedSources_WorkWithTheThreeParameterProperty()
        {
            using var hp = new ModifiableProperty<int, object, Source>(100, new ValueBounds<int>(0, 999));
            var poison = new Source("poison", 1);

            hp.PushModifier(v => v - 10, source: poison);
            hp.PushModifier(v => v - 5, source: new Source("poison", 2));

            Assert.AreEqual(85, hp.CurrentValue);
            Assert.AreEqual(1, hp.RemoveModifiers(new Source("poison", 1))); // value equality
            Assert.AreEqual(95, hp.CurrentValue);
        }

        private sealed record Source(string Status, int Instance);

        [Test]
        public void Dispose_IsIdempotent()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            property.Dispose();

            Assert.DoesNotThrow(property.Dispose);
        }
    }
}
