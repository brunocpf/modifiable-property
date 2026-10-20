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

            using (atk.PushModifier(new ValueModifier<float>("atk-mod", baseValue => baseValue + 10f)))
            {
                Assert.AreEqual(25f, atk.CurrentValue);
                Assert.AreEqual(15f, atk.Base.CurrentValue);

                atk.AddDelta(+20f);
                Assert.AreEqual(45f, atk.CurrentValue);
                Assert.AreEqual(35f, atk.Base.CurrentValue);

                using (atk.PushFilter("atk-increase-boost", delta =>
                       delta.Delta > 0
                           ? new ValueDelta<float, object>(delta.Delta * 1.5f, delta.Context)
                           : delta))
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

                using (level.PushModifier(new ValueModifier<int>("fixed-level-2", _ => 2)))
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

            using var _overflow = hp.PushFilter("hp-overflow-protection", valueDelta =>
            {
                int max = maxHp.CurrentValue;
                int newHp = hp.CurrentValue + valueDelta.Delta;

                if (newHp > max)
                {
                    return new ValueDelta<int, object>(max - hp.CurrentValue, valueDelta.Context);
                }

                return valueDelta;
            });

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
            using var modHandle = maxHp.PushModifier(new ValueModifier<int>("disease_cap", v => Math.Min(v, cap)));
            using var damageSub = hp.ProcessedDeltas.Subscribe(delta =>
            {
                if (delta.Delta < 0)
                {
                    cap = hp.CurrentValue;
                    maxHp.AddOrUpdateModifier(new ValueModifier<int>("disease_cap", v => Math.Min(v, cap)));
                }
            });

            Assert.AreEqual(200, maxHp.CurrentValue);

            hp.AddDelta(+50);
            Assert.AreEqual(200, hp.CurrentValue);
            Assert.AreEqual(200, maxHp.CurrentValue);

            hp.AddDelta(-100);
            Assert.AreEqual(100, hp.CurrentValue);
            Assert.AreEqual(100, maxHp.CurrentValue);

            modHandle.Dispose();
            damageSub.Dispose();

            Assert.AreEqual(500, maxHp.CurrentValue);
        }

        [Test]
        public void RemoveModifier_AfterDispose_DoesNotThrow()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            property.AddOrUpdateModifier("buff", v => v + 10);
            property.Dispose();

            Assert.DoesNotThrow(() => property.RemoveModifier("buff"));
        }

        [Test]
        public void DisposingPushModifierHandle_AfterPropertyDisposed_DoesNotThrow()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            IDisposable handle = property.PushModifier("buff", v => v + 10);
            property.Dispose();

            Assert.DoesNotThrow(handle.Dispose);
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
                property.AddOrUpdateModifier($"m{id}", v => (v * 31 + id) % Mod);
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
                    property.AddOrUpdateModifier($"m{id}", v => (v * 31 + id) % Mod, priority);
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

            foreach (int id in order)
            {
                int captured = id;
                property.AddOrUpdateModifier($"m{id}", v => (v * 31 + captured) % Mod);
            }

            property.RemoveModifier("m5");
            property.AddOrUpdateModifier("m5", v => (v * 31 + 5) % Mod);
            order.Remove(5);
            order.Add(5);

            Assert.AreEqual(Fold(7, order.ToArray()), property.CurrentValue);
        }

        [Test]
        public void Modifiers_ReplaceWithSameId_KeepsPosition()
        {
            using var property = NewLongProperty();

            for (int i = 1; i <= ManyEntries; i++)
            {
                int id = i;
                property.AddOrUpdateModifier($"m{id}", v => (v * 31 + id) % Mod);
            }

            // Replacement uses a different function but must stay in the same slot.
            property.AddOrUpdateModifier("m3", v => (v * 31 + 1000) % Mod);

            int[] expected = Enumerable.Range(1, ManyEntries).Select(i => i == 3 ? 1000 : i).ToArray();
            Assert.AreEqual(Fold(7, expected), property.CurrentValue);
        }

        [Test]
        public void Modifiers_ReplaceWithNewPriority_MovesToEndOfNewPriority()
        {
            using var property = NewLongProperty();

            for (int i = 1; i <= ManyEntries; i++)
            {
                int id = i;
                property.AddOrUpdateModifier($"m{id}", v => (v * 31 + id) % Mod);
            }

            property.AddOrUpdateModifier("m2", v => (v * 31 + 2) % Mod, priority: 1);

            int[] expected = Enumerable.Range(1, ManyEntries).Where(i => i != 2).Append(2).ToArray();
            Assert.AreEqual(Fold(7, expected), property.CurrentValue);
        }

        [Test]
        public void Modifiers_InitialModifiers_AreStablySortedByPriority()
        {
            var initial = Enumerable.Range(1, ManyEntries)
                .Select(i => new ValueModifier<long>($"m{i}", v => (v * 31 + i) % Mod, i % 3))
                .ToList();

            using ModifiableProperty<long, object> property = new(
                7, new ValueBounds<long>(long.MinValue, long.MaxValue), initial);

            int[] expected = Enumerable.Range(1, ManyEntries).OrderBy(i => i % 3).ToArray();
            Assert.AreEqual(Fold(7, expected), property.CurrentValue);
        }

        [Test]
        public void Modifiers_ClassicAddThenMultiply_FollowsInsertionOrder()
        {
            using ModifiableProperty<float, object> property = new(10f, 0f, 999f);

            property.AddOrUpdateModifier("add", v => v + 10f);
            property.AddOrUpdateModifier("mul", v => v * 2f);

            Assert.AreEqual(40f, property.CurrentValue);
        }

        [Test]
        public void Filters_WithEqualPriority_RunInInsertionOrder()
        {
            using var property = NewLongProperty(0);

            for (int i = 1; i <= ManyEntries; i++)
            {
                int id = i;
                property.AddOrUpdateFilter($"f{id}", d => new ValueDelta<long, object>((d.Delta * 31 + id) % Mod, d.Context));
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
                    property.AddOrUpdateFilter(
                        $"f{id}",
                        d => new ValueDelta<long, object>((d.Delta * 31 + id) % Mod, d.Context),
                        priority);
                }

                property.AddDelta(5);

                int[] expectedOrder = added.OrderBy(a => a.Priority).Select(a => a.Id).ToArray();
                Assert.AreEqual(Fold(5, expectedOrder), property.Base.CurrentValue, $"run {run}");
            }
        }

        [Test]
        public void Filters_ReplaceWithSameId_KeepsPosition()
        {
            using var property = NewLongProperty(0);

            for (int i = 1; i <= ManyEntries; i++)
            {
                int id = i;
                property.AddOrUpdateFilter($"f{id}", d => new ValueDelta<long, object>((d.Delta * 31 + id) % Mod, d.Context));
            }

            property.AddOrUpdateFilter("f3", d => new ValueDelta<long, object>((d.Delta * 31 + 1000) % Mod, d.Context));
            property.AddDelta(5);

            int[] expected = Enumerable.Range(1, ManyEntries).Select(i => i == 3 ? 1000 : i).ToArray();
            Assert.AreEqual(Fold(5, expected), property.Base.CurrentValue);
        }

        [Test]
        public void RemoveFilter_AfterDispose_DoesNotThrow()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            property.AddOrUpdateFilter("noop", delta => delta);
            property.Dispose();

            Assert.DoesNotThrow(() => property.RemoveFilter("noop"));
        }

        [Test]
        public void ClearFilters_AfterDispose_DoesNotThrow()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            property.AddOrUpdateFilter("noop", delta => delta);
            property.Dispose();

            Assert.DoesNotThrow(property.ClearFilters);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            ModifiableProperty<int, object> property = new(100, new ValueBounds<int>(0, 1000));
            property.Dispose();

            Assert.DoesNotThrow(property.Dispose);
        }
    }
}
