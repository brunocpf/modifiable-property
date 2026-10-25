using System;
using BrunoCPF.Modifiable.Common.Math;
using BrunoCPF.Modifiable.Common.Properties;
using BrunoCPF.Modifiable.Core;
using NUnit.Framework;

namespace BrunoCPF.Modifiable.Tests
{
    public class ValueMathTests
    {
        [Test]
        public void IntMath_SaturatesInsteadOfWrapping()
        {
            var math = new IntValueMath();

            Assert.AreEqual(int.MaxValue, math.Add(int.MaxValue - 1, 10));
            Assert.AreEqual(int.MinValue, math.Add(int.MinValue + 1, -10));
            Assert.AreEqual(int.MinValue, math.Subtract(int.MinValue + 1, 10));
            Assert.AreEqual(int.MaxValue, math.Subtract(int.MaxValue - 1, -10));
        }

        [Test]
        public void LongMath_SaturatesInsteadOfWrapping()
        {
            var math = new LongValueMath();

            Assert.AreEqual(long.MaxValue, math.Add(long.MaxValue - 1, 10));
            Assert.AreEqual(long.MinValue, math.Add(long.MinValue + 1, -10));
            Assert.AreEqual(long.MinValue, math.Subtract(long.MinValue + 1, 10));
            Assert.AreEqual(long.MaxValue, math.Subtract(long.MaxValue - 1, -10));
            Assert.AreEqual(-1L, math.Add(long.MaxValue, long.MinValue));
        }

        [Test]
        public void IntProperty_NearTheLimitSaturatesInsteadOfWrapping()
        {
            using var exp = new ModifiableProperty<int, object>(int.MaxValue - 1, ValueBounds<int>.AtLeast(0));

            exp.AddDelta(10);

            Assert.AreEqual(int.MaxValue, exp.CurrentValue);
        }

        [Test]
        public void FloatingPoint_InfinitiesSaturateAndNaNThrows()
        {
            Assert.AreEqual(float.MaxValue, new FloatValueMath().Add(float.MaxValue, float.MaxValue));
            Assert.AreEqual(double.MinValue, new DoubleValueMath().Add(double.MinValue, double.MinValue));
            Assert.AreEqual(float.MaxValue, new FloatValueMath().FromDouble(1e300));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DoubleValueMath().FromDouble(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FloatValueMath().Add(float.NaN, 1f));
        }

        [Test]
        public void BuiltInOperations_NeverProduceNaNOrInfinity()
        {
            Modifier<string>[] modifiers =
            {
                new Modifier<string>(ModifierOperation.Multiply, 10),
                new Modifier<string>(ModifierOperation.Multiply, 0),
            };

            // 1e308 × 10 saturates to double.MaxValue, so × 0 gives 0 instead of NaN.
            Assert.AreEqual(0d, ModifierStack.Evaluate(1e308, modifiers));
            Assert.AreEqual(float.MaxValue, ModifierStack.Evaluate(3e38f, new[] { new Modifier<string>(ModifierOperation.Multiply, 10) }));
        }

        [Test]
        public void FloatFilters_CannotStoreInfinity()
        {
            using var value = new ModifiableProperty<float, object, string>(1f);
            value.PushFilter(new DeltaFilter<string>(DeltaOperation.Scale, 1e30));

            value.AddDelta(1e30f);

            Assert.AreEqual(float.MaxValue, value.Base.CurrentValue);
        }

        [Test]
        public void LongsNearTheLimits_SurviveIntegralAndNoOpOperations()
        {
            const long nearMax = long.MaxValue - 1;
            Modifier<string>[] noOps =
            {
                new Modifier<string>(ModifierOperation.Add, 0),
                new Modifier<string>(ModifierOperation.Multiply, 1),
                new Modifier<string>(ModifierOperation.AtLeast, 0),
                new Modifier<string>(ModifierOperation.AtMost, 9.3e18),
            };

            Assert.AreEqual(nearMax, ModifierStack.Evaluate(nearMax, noOps));
            Assert.AreEqual(nearMax - 5, ModifierStack.Evaluate(nearMax, new[] { new Modifier<string>(ModifierOperation.Add, -5) }));
            Assert.AreEqual(long.MaxValue, ModifierStack.Evaluate(nearMax, new[] { new Modifier<string>(ModifierOperation.Add, 5) }));
            Assert.AreEqual(long.MinValue + 3, ModifierStack.Evaluate(long.MinValue + 1, new[] { new Modifier<string>(ModifierOperation.Add, 2) }));
        }

        [Test]
        public void LongFilters_SurviveIntegralAndNoOpOperations()
        {
            const long huge = long.MaxValue - 1;
            DeltaFilter<string>[] noOps =
            {
                new DeltaFilter<string>(DeltaOperation.Scale, 1),
                new DeltaFilter<string>(DeltaOperation.Add, 0),
                new DeltaFilter<string>(DeltaOperation.CapMagnitude, 9.3e18),
            };

            DeltaResult<long, string> result = DeltaPipeline.Apply(0L, huge, noOps);

            Assert.AreEqual(huge, result.After);
            Assert.IsEmpty(result.FiredFilters);
        }

        [Test]
        public void FractionalAdds_StillRoundAfterTheSum()
        {
            // -10 + 2.5 = -7.5 → -7 toward zero (not -10 + 2 = -8)
            Assert.AreEqual(-7, ModifierStack.Evaluate(-10, new[] { new Modifier<string>(ModifierOperation.Add, 2.5) }));
        }
    }
}
