using System;
using BrunoCPF.Modifiable.Common.Properties;
using NUnit.Framework;

namespace BrunoCPF.Modifiable.Tests
{
    public class ValueBoundsTests
    {
        [Test]
        public void DefaultBounds_DoNotClampValueTypes()
        {
            var bounds = new ValueBounds<int>();

            Assert.AreEqual(50, bounds.Clamp(50));
            Assert.AreEqual(-50, bounds.Clamp(-50));
            Assert.IsFalse(bounds.HasMin);
            Assert.IsFalse(bounds.HasMax);
            Assert.AreEqual(ValueBounds<int>.Unbounded, bounds);
        }

        [Test]
        public void MinOnly_LeavesTheMaximumUnbounded()
        {
            var bounds = new ValueBounds<int>(1);

            Assert.AreEqual(1, bounds.Clamp(-5));
            Assert.AreEqual(5000, bounds.Clamp(5000));
            Assert.AreEqual(bounds, ValueBounds<int>.AtLeast(1));
        }

        [Test]
        public void AtMost_LeavesTheMinimumUnbounded()
        {
            var bounds = ValueBounds<int>.AtMost(10);

            Assert.AreEqual(-5000, bounds.Clamp(-5000));
            Assert.AreEqual(10, bounds.Clamp(11));
            Assert.IsFalse(bounds.Contains(11));
            Assert.IsTrue(bounds.Contains(-11));
        }

        [Test]
        public void ZeroIsAValidExplicitBound()
        {
            var bounds = new ValueBounds<int>(0, 0);

            Assert.AreEqual(0, bounds.Clamp(7));
            Assert.AreEqual(0, bounds.Clamp(-7));
        }

        [Test]
        public void NullReferenceTypeBounds_AreUnbounded()
        {
            var bounds = new ValueBounds<string>(null!, "m");

            Assert.AreEqual("a", bounds.Clamp("a"));
            Assert.AreEqual("m", bounds.Clamp("z"));
        }

        [Test]
        public void MinGreaterThanMax_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ValueBounds<int>(10, 1));
        }

        [Test]
        public void Property_WithoutBoundsIsUnbounded()
        {
            using ModifiableProperty<int, object> value = new(100);
            using ModifiableProperty<int, object> explicitNull = new(100, bounds: null);

            value.AddDelta(-250);
            explicitNull.AddDelta(+250);

            Assert.AreEqual(-150, value.CurrentValue);
            Assert.AreEqual(350, explicitNull.CurrentValue);
        }

        [Test]
        public void Property_WithOnlyAMinimumKeepsItsMaximumOpen()
        {
            using ModifiableProperty<int, object> attack = new(10, min: 1);

            attack.AddDelta(+990);
            Assert.AreEqual(1000, attack.CurrentValue);

            attack.AddDelta(-5000);
            Assert.AreEqual(1, attack.CurrentValue);
        }

        [Test]
        public void Property_WithOnlyAMaximumKeepsItsMinimumOpen()
        {
            using ModifiableProperty<int, object> debt = new(0, max: 0);

            debt.AddDelta(-75);
            Assert.AreEqual(-75, debt.CurrentValue);

            debt.AddDelta(+500);
            Assert.AreEqual(0, debt.CurrentValue);
        }

        [Test]
        public void Property_WithMinAndMaxClamps()
        {
            using ModifiableProperty<int, object> hp = new(50, min: 0, max: 100);

            hp.AddDelta(-200);
            Assert.AreEqual(0, hp.CurrentValue);

            hp.AddDelta(+999);
            Assert.AreEqual(100, hp.CurrentValue);
        }
    }
}
