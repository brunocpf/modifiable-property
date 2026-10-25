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
            Assert.AreEqual(15, ModifierStack.Evaluate(10, new[] { new Modifier<string>(ModifierOperation.Add, 5, Source: "a") }));
            Assert.AreEqual(25, ModifierStack.Evaluate(10, new[] { new Modifier<string>(ModifierOperation.Multiply, 2.5, Source: "m") }));
            Assert.AreEqual(3, ModifierStack.Evaluate(10, new[] { new Modifier<string>(ModifierOperation.Override, 3, Source: "o") }));
            Assert.AreEqual(12, ModifierStack.Evaluate(10, new[] { new Modifier<string>(ModifierOperation.AtLeast, 12, Source: "l") }));
            Assert.AreEqual(10, ModifierStack.Evaluate(10, new[] { new Modifier<string>(ModifierOperation.AtLeast, 8, Source: "l") }));
            Assert.AreEqual(7, ModifierStack.Evaluate(10, new[] { new Modifier<string>(ModifierOperation.AtMost, 7, Source: "u") }));
        }

        [Test]
        public void Evaluate_RunsByPriorityThenGivenOrder()
        {
            Modifier<string>[] modifiers =
            {
                new Modifier<string>(ModifierOperation.Multiply, 2, Priority: 10, Source: "double"),
                new Modifier<string>(ModifierOperation.Add, 5, Priority: 0, Source: "plus-five"),
                new Modifier<string>(ModifierOperation.Add, 1, Priority: 10, Source: "plus-one"),
            };

            // (10 + 5) * 2 + 1
            Assert.AreEqual(31, ModifierStack.Evaluate(10, modifiers));

            // Equal priorities keep the given order: + 1 before * 2.
            Modifier<string>[] swapped = { modifiers[1], modifiers[2], modifiers[0] };
            Assert.AreEqual(32, ModifierStack.Evaluate(10, swapped));
        }

        [Test]
        public void Evaluate_IntegralTypesRoundPerStepTowardZeroByDefault()
        {
            Modifier<string>[] modifiers =
            {
                new Modifier<string>(ModifierOperation.Multiply, 1.5, Source: "first"),
                new Modifier<string>(ModifierOperation.Multiply, 1.5, Source: "second"),
            };

            // 5 * 1.5 = 7.5 -> 7; 7 * 1.5 = 10.5 -> 10
            Assert.AreEqual(10, ModifierStack.Evaluate(5, modifiers));
            Assert.AreEqual(-7, ModifierStack.Evaluate(-5, new[] { new Modifier<string>(ModifierOperation.Multiply, 1.5, Source: "m") }));
        }

        [TestCase(RoundingMode.TowardZero, 7, -7)]
        [TestCase(RoundingMode.Floor, 7, -8)]
        [TestCase(RoundingMode.Ceiling, 8, -7)]
        [TestCase(RoundingMode.Nearest, 8, -8)]
        public void Evaluate_HonoursRoundingMode(RoundingMode mode, int positive, int negative)
        {
            var math = new IntValueMath(mode);
            Modifier<string>[] scale = { new Modifier<string>(ModifierOperation.Multiply, 1.5, Source: "m") };

            Assert.AreEqual(positive, ModifierStack.Evaluate(5, scale, math));
            Assert.AreEqual(negative, ModifierStack.Evaluate(-5, scale, math));
        }

        [Test]
        public void Evaluate_SaturatesIntegralResults()
        {
            Assert.AreEqual(int.MaxValue, ModifierStack.Evaluate(int.MaxValue, new[] { new Modifier<string>(ModifierOperation.Multiply, 4, Source: "m") }));
            Assert.AreEqual(long.MinValue, ModifierStack.Evaluate(long.MinValue, new[] { new Modifier<string>(ModifierOperation.Multiply, 4, Source: "m") }));
        }

        [Test]
        public void Evaluate_FloatsKeepFractions()
        {
            Assert.AreEqual(7.5f, ModifierStack.Evaluate(5f, new[] { new Modifier<string>(ModifierOperation.Multiply, 1.5, Source: "m") }));
        }

        [Test]
        public void Evaluate_StackGroupsDefaultToStacking()
        {
            Modifier<string>[] boosts =
            {
                new Modifier<string>(ModifierOperation.Multiply, 1.5, StackGroup: "boost", Source: "boost-a"),
                new Modifier<string>(ModifierOperation.Multiply, 2, StackGroup: "boost", Source: "boost-b"),
            };

            Assert.AreEqual(300, ModifierStack.Evaluate(100, boosts));
        }

        [Test]
        public void Evaluate_HighestOnlyKeepsTheLargestOperandInTheGroup()
        {
            Modifier<string>[] modifiers =
            {
                new Modifier<string>(ModifierOperation.Multiply, 1.5, StackGroup: "boost", Source: "boost-a"),
                new Modifier<string>(ModifierOperation.Multiply, 2, StackGroup: "boost", Source: "boost-b"),
                new Modifier<string>(ModifierOperation.Add, 10, Source: "flat"),
            };

            int result = ModifierStack.Evaluate(100, modifiers, stacking: group => group == "boost" ? StackPolicy.HighestOnly : StackPolicy.Stack);

            Assert.AreEqual(210, result);
        }

        [Test]
        public void Evaluate_LowestOnlyKeepsTheSmallestOperandAndTiesKeepTheEarliest()
        {
            Modifier<string>[] modifiers =
            {
                new Modifier<string>(ModifierOperation.AtMost, 50, StackGroup: "cap", Source: "cap-a"),
                new Modifier<string>(ModifierOperation.AtMost, 50, StackGroup: "cap", Source: "cap-b"),
                new Modifier<string>(ModifierOperation.AtMost, 80, StackGroup: "cap", Source: "cap-c"),
            };

            ModifierBreakdown<int, string> breakdown = ModifierStack.Explain(100, modifiers, stacking: _ => StackPolicy.LowestOnly);

            Assert.AreEqual(50, breakdown.Result);
            CollectionAssert.AreEqual(
                new[] { false, true, true },
                breakdown.Steps.Select(step => step.Suppressed).ToArray());
        }

        [Test]
        public void Explain_ReportsEveryStep()
        {
            Modifier<string>[] modifiers =
            {
                new Modifier<string>(ModifierOperation.Add, 10, Source: "sword"),
                new Modifier<string>(ModifierOperation.Multiply, 1.2, Priority: 1, Source: "boost"),
            };

            ModifierBreakdown<int, string> breakdown = ModifierStack.Explain(40, modifiers);

            Assert.AreEqual(40, breakdown.Base);
            Assert.AreEqual(60, breakdown.Result);
            Assert.AreEqual(2, breakdown.Steps.Count);
            Assert.AreEqual(("sword", 40, 50), (breakdown.Steps[0].Source, breakdown.Steps[0].Before, breakdown.Steps[0].After));
            Assert.AreEqual(("boost", 50, 60), (breakdown.Steps[1].Source, breakdown.Steps[1].Before, breakdown.Steps[1].After));
            Assert.AreSame(modifiers[1], breakdown.Steps[1].Data);
        }

        [Test]
        public void Modifiers_NeedNoIdentity_EqualModifiersBothApply()
        {
            var plusFive = new Modifier<string>(ModifierOperation.Add, 5, Source: "buff");

            Assert.AreEqual(20, ModifierStack.Evaluate(10, new[] { plusFive, plusFive }));
        }

        [Test]
        public void Modifiers_CanHaveNoSource()
        {
            ModifierBreakdown<int, string> breakdown = ModifierStack.Explain(1, new[] { new Modifier<string>(ModifierOperation.Add, 1) });

            Assert.AreEqual(2, breakdown.Result);
            Assert.IsNull(breakdown.Steps[0].Source);
        }

        [Test]
        public void Sources_CanBeAnyType()
        {
            var tarukaja = new BuffSource("tarukaja", 3);
            Modifier<BuffSource>[] modifiers = { new Modifier<BuffSource>(ModifierOperation.Multiply, 1.5, Source: tarukaja) };

            ModifierBreakdown<int, BuffSource> breakdown = ModifierStack.Explain(40, modifiers);

            Assert.AreEqual(60, breakdown.Result);
            Assert.AreEqual(new BuffSource("tarukaja", 3), breakdown.Steps[0].Source);
        }

        private sealed record BuffSource(string Status, int Instance);

        [Test]
        public void NonStackingGroups_MustShareOneOperation()
        {
            Modifier<string>[] mixed =
            {
                new Modifier<string>(ModifierOperation.Multiply, 2, StackGroup: "boost"),
                new Modifier<string>(ModifierOperation.Add, 5, StackGroup: "boost"),
            };

            Assert.Throws<InvalidOperationException>(() => ModifierStack.Evaluate(100, mixed, stacking: _ => StackPolicy.HighestOnly));
            Assert.AreEqual(205, ModifierStack.Evaluate(100, mixed), "stacking groups may mix operations");
        }

        [Test]
        public void NonStackingGroups_MustShareOneCustomName()
        {
            var operations = new CustomOperations()
                .AddModifierOperation("a", static (v, _) => v)
                .AddModifierOperation("b", static (v, _) => v);
            Modifier<string>[] mixed =
            {
                new Modifier<string>(ModifierOperation.Custom, 1, StackGroup: "g", CustomName: "a"),
                new Modifier<string>(ModifierOperation.Custom, 2, StackGroup: "g", CustomName: "b"),
            };

            Assert.Throws<InvalidOperationException>(() =>
                ModifierStack.Evaluate(1, mixed, stacking: _ => StackPolicy.LowestOnly, operations: operations));
        }

        [Test]
        public void Penalties_UseLowestOnlyToKeepTheStrongest()
        {
            Modifier<string>[] debuffs =
            {
                new Modifier<string>(ModifierOperation.Add, -30, StackGroup: "atk-down"),
                new Modifier<string>(ModifierOperation.Add, -10, StackGroup: "atk-down"),
            };

            Assert.AreEqual(70, ModifierStack.Evaluate(100, debuffs, stacking: _ => StackPolicy.LowestOnly));
            Assert.AreEqual(90, ModifierStack.Evaluate(100, debuffs, stacking: _ => StackPolicy.HighestOnly));
        }

        [Test]
        public void With_WorksOnEveryMemberAndStillValidates()
        {
            var modifier = new Modifier<string>(ModifierOperation.Add, 1, Source: "a");

            Modifier<string> changed = modifier with { Value = 2, Priority = 3, StackGroup = "g", Source = "b" };

            Assert.AreEqual((2d, 3, "g", "b"), (changed.Value, changed.Priority, changed.StackGroup, changed.Source));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = modifier with { Value = double.NaN });
            Assert.Throws<ArgumentException>(() => _ = modifier with { StackGroup = " " });
        }

        [Test]
        public void With_InconsistentCustomPairing_IsCaughtAtEvaluation()
        {
            Modifier<string> custom = new Modifier<string>(ModifierOperation.Add, 1) with { Operation = ModifierOperation.Custom };
            Modifier<string> named = new Modifier<string>(ModifierOperation.Add, 1) with { CustomName = "soft-cap" };

            Assert.Throws<InvalidOperationException>(() => ModifierStack.Evaluate(1, new[] { custom }));
            Assert.Throws<InvalidOperationException>(() => ModifierStack.Evaluate(1, new[] { named }));
        }

        [Test]
        public void Evaluate_RequiresScalableMathForUnknownTypes()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ModifierStack.Evaluate(TimeSpan.Zero, new[] { new Modifier<string>(ModifierOperation.Add, 1, Source: "a") }));
        }

        [Test]
        public void Modifier_ValidatesItsData()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Modifier<string>((ModifierOperation)99, 1, Source: "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Modifier<string>(ModifierOperation.Add, double.NaN, Source: "a"));
            Assert.Throws<ArgumentException>(() => _ = new Modifier<string>(ModifierOperation.Add, 1, StackGroup: "", Source: "a"));
        }

        [Test]
        public void Modifier_HasValueEquality()
        {
            Assert.AreEqual(
                new Modifier<string>(ModifierOperation.Multiply, 1.5, 2, "g", Source: "a"),
                new Modifier<string>(ModifierOperation.Multiply, 1.5, 2, "g", Source: "a"));
        }
    }
}
