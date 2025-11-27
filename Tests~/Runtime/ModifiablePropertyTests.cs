using System;
using BrunoCPF.Modifiable.Common.Properties;
using NUnit.Framework;
using R3;

namespace BrunoCPF.Modifiable.Tests.Runtime
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
    }
}
