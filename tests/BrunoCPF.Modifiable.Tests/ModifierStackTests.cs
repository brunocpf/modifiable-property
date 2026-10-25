using System;
using System.Linq;
using BrunoCPF.Modifiable.Common.Math;
using BrunoCPF.Modifiable.Core;
using NUnit.Framework;

namespace BrunoCPF.Modifiable.Tests
{
    public class ModifierStackTests
    {
        [Test]
        public void Evaluate_AppliesEachOperation()
        {
            Assert.AreEqual(15, ModifierStack.Evaluate(10, new[] { new Modifier("a", ModifierOperation.Add, 5) }));
            Assert.AreEqual(25, ModifierStack.Evaluate(10, new[] { new Modifier("m", ModifierOperation.Multiply, 2.5) }));
            Assert.AreEqual(3, ModifierStack.Evaluate(10, new[] { new Modifier("o", ModifierOperation.Override, 3) }));
            Assert.AreEqual(12, ModifierStack.Evaluate(10, new[] { new Modifier("l", ModifierOperation.AtLeast, 12) }));
            Assert.AreEqual(10, ModifierStack.Evaluate(10, new[] { new Modifier("l", ModifierOperation.AtLeast, 8) }));
            Assert.AreEqual(7, ModifierStack.Evaluate(10, new[] { new Modifier("u", ModifierOperation.AtMost, 7) }));
        }

        [Test]
        public void Evaluate_RunsByPriorityThenGivenOrder()
        {
            Modifier[] modifiers =
            {
                new Modifier("double", ModifierOperation.Multiply, 2, Priority: 10),
                new Modifier("plus-five", ModifierOperation.Add, 5, Priority: 0),
                new Modifier("plus-one", ModifierOperation.Add, 1, Priority: 10),
            };

            // (10 + 5) * 2 + 1
            Assert.AreEqual(31, ModifierStack.Evaluate(10, modifiers));

            // Equal priorities keep the given order: + 1 before * 2.
            Modifier[] swapped = { modifiers[1], modifiers[2], modifiers[0] };
            Assert.AreEqual(32, ModifierStack.Evaluate(10, swapped));
        }

        [Test]
        public void Evaluate_IntegralTypesRoundPerStepTowardZeroByDefault()
        {
            Modifier[] modifiers =
            {
                new Modifier("first", ModifierOperation.Multiply, 1.5),
                new Modifier("second", ModifierOperation.Multiply, 1.5),
            };

            // 5 * 1.5 = 7.5 -> 7; 7 * 1.5 = 10.5 -> 10
            Assert.AreEqual(10, ModifierStack.Evaluate(5, modifiers));
            Assert.AreEqual(-7, ModifierStack.Evaluate(-5, new[] { new Modifier("m", ModifierOperation.Multiply, 1.5) }));
        }

        [TestCase(RoundingMode.TowardZero, 7, -7)]
        [TestCase(RoundingMode.Floor, 7, -8)]
        [TestCase(RoundingMode.Ceiling, 8, -7)]
        [TestCase(RoundingMode.Nearest, 8, -8)]
        public void Evaluate_HonoursRoundingMode(RoundingMode mode, int positive, int negative)
        {
            var math = new IntValueMath(mode);
            Modifier[] scale = { new Modifier("m", ModifierOperation.Multiply, 1.5) };

            Assert.AreEqual(positive, ModifierStack.Evaluate(5, scale, math));
            Assert.AreEqual(negative, ModifierStack.Evaluate(-5, scale, math));
        }

        [Test]
        public void Evaluate_SaturatesIntegralResults()
        {
            Assert.AreEqual(int.MaxValue, ModifierStack.Evaluate(int.MaxValue, new[] { new Modifier("m", ModifierOperation.Multiply, 4) }));
            Assert.AreEqual(long.MinValue, ModifierStack.Evaluate(long.MinValue, new[] { new Modifier("m", ModifierOperation.Multiply, 4) }));
        }

        [Test]
        public void Evaluate_FloatsKeepFractions()
        {
            Assert.AreEqual(7.5f, ModifierStack.Evaluate(5f, new[] { new Modifier("m", ModifierOperation.Multiply, 1.5) }));
        }

        [Test]
        public void Evaluate_StackGroupsDefaultToStacking()
        {
            Modifier[] boosts =
            {
                new Modifier("boost-a", ModifierOperation.Multiply, 1.5, StackGroup: "boost"),
                new Modifier("boost-b", ModifierOperation.Multiply, 2, StackGroup: "boost"),
            };

            Assert.AreEqual(300, ModifierStack.Evaluate(100, boosts));
        }

        [Test]
        public void Evaluate_HighestOnlyKeepsTheLargestOperandInTheGroup()
        {
            Modifier[] modifiers =
            {
                new Modifier("boost-a", ModifierOperation.Multiply, 1.5, StackGroup: "boost"),
                new Modifier("boost-b", ModifierOperation.Multiply, 2, StackGroup: "boost"),
                new Modifier("flat", ModifierOperation.Add, 10),
            };

            int result = ModifierStack.Evaluate(100, modifiers, stacking: group => group == "boost" ? StackPolicy.HighestOnly : StackPolicy.Stack);

            Assert.AreEqual(210, result);
        }

        [Test]
        public void Evaluate_LowestOnlyKeepsTheSmallestOperandAndTiesKeepTheEarliest()
        {
            Modifier[] modifiers =
            {
                new Modifier("cap-a", ModifierOperation.AtMost, 50, StackGroup: "cap"),
                new Modifier("cap-b", ModifierOperation.AtMost, 50, StackGroup: "cap"),
                new Modifier("cap-c", ModifierOperation.AtMost, 80, StackGroup: "cap"),
            };

            ModifierBreakdown<int> breakdown = ModifierStack.Explain(100, modifiers, stacking: _ => StackPolicy.LowestOnly);

            Assert.AreEqual(50, breakdown.Result);
            CollectionAssert.AreEqual(
                new[] { false, true, true },
                breakdown.Steps.Select(step => step.Suppressed).ToArray());
        }

        [Test]
        public void Explain_ReportsEveryStep()
        {
            Modifier[] modifiers =
            {
                new Modifier("sword", ModifierOperation.Add, 10),
                new Modifier("boost", ModifierOperation.Multiply, 1.2, Priority: 1),
            };

            ModifierBreakdown<int> breakdown = ModifierStack.Explain(40, modifiers);

            Assert.AreEqual(40, breakdown.Base);
            Assert.AreEqual(60, breakdown.Result);
            Assert.AreEqual(2, breakdown.Steps.Count);
            Assert.AreEqual(("sword", 40, 50), (breakdown.Steps[0].Id, breakdown.Steps[0].Before, breakdown.Steps[0].After));
            Assert.AreEqual(("boost", 50, 60), (breakdown.Steps[1].Id, breakdown.Steps[1].Before, breakdown.Steps[1].After));
            Assert.AreSame(modifiers[1], breakdown.Steps[1].Data);
        }

        [Test]
        public void Evaluate_RejectsDuplicateIds()
        {
            Modifier[] modifiers =
            {
                new Modifier("same", ModifierOperation.Add, 1),
                new Modifier("same", ModifierOperation.Add, 2),
            };

            Assert.Throws<ArgumentException>(() => ModifierStack.Evaluate(0, modifiers));
        }

        [Test]
        public void Evaluate_RequiresScalableMathForUnknownTypes()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ModifierStack.Evaluate(TimeSpan.Zero, new[] { new Modifier("a", ModifierOperation.Add, 1) }));
        }

        [Test]
        public void Modifier_ValidatesItsData()
        {
            Assert.Throws<ArgumentException>(() => _ = new Modifier(" ", ModifierOperation.Add, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Modifier("a", (ModifierOperation)99, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Modifier("a", ModifierOperation.Add, double.NaN));
            Assert.Throws<ArgumentException>(() => _ = new Modifier("a", ModifierOperation.Add, 1, StackGroup: ""));
        }

        [Test]
        public void Modifier_HasValueEquality()
        {
            Assert.AreEqual(
                new Modifier("a", ModifierOperation.Multiply, 1.5, 2, "g"),
                new Modifier("a", ModifierOperation.Multiply, 1.5, 2, "g"));
        }
    }
}
