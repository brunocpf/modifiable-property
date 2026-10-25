using System;
using BrunoCPF.Modifiable.Common.Properties;
using BrunoCPF.Modifiable.Core;
using NUnit.Framework;

namespace BrunoCPF.Modifiable.Tests
{
    public class DeltaPipelineTests
    {
        [Test]
        public void Apply_RunsFiltersThenBounds()
        {
            DeltaFilter[] filters =
            {
                new DeltaFilter("guard", DeltaOperation.Scale, 0.5, DeltaDirection.Decrease),
                new DeltaFilter("cap", DeltaOperation.CapMagnitude, 30, Priority: 1),
            };

            DeltaResult<int> result = DeltaPipeline.Apply(40, -100, filters, new ValueBounds<int>(0, 100));

            Assert.AreEqual(40, result.Before);
            Assert.AreEqual(-100, result.Requested);
            Assert.AreEqual(-30, result.Filtered);
            Assert.AreEqual(10, result.After);
            Assert.AreEqual(-30, result.Applied);
            CollectionAssert.AreEqual(new[] { "guard", "cap" }, result.FiredFilters);
        }

        [Test]
        public void Apply_ReportsTheChangeActuallyMadeAfterBounds()
        {
            DeltaResult<int> result = DeltaPipeline.Apply(10, -25, Array.Empty<DeltaFilter>(), new ValueBounds<int>(0, 100));

            Assert.AreEqual(-25, result.Filtered);
            Assert.AreEqual(0, result.After);
            Assert.AreEqual(-10, result.Applied);
            Assert.IsEmpty(result.FiredFilters);
        }

        [Test]
        public void Apply_WithoutBoundsDoesNotClamp()
        {
            DeltaResult<int> result = DeltaPipeline.Apply(10, -25, Array.Empty<DeltaFilter>());

            Assert.AreEqual(-15, result.After);
        }

        [Test]
        public void Filters_OnlyApplyToMatchingDirections()
        {
            DeltaFilter[] filters = { new DeltaFilter("heal-block", DeltaOperation.Block, AppliesTo: DeltaDirection.Increase) };

            DeltaResult<int> heal = DeltaPipeline.Apply(50, 20, filters);
            DeltaResult<int> damage = DeltaPipeline.Apply(50, -20, filters);

            Assert.AreEqual(50, heal.After);
            CollectionAssert.AreEqual(new[] { "heal-block" }, heal.FiredFilters);
            Assert.AreEqual(30, damage.After);
            Assert.IsEmpty(damage.FiredFilters);
        }

        [Test]
        public void ZeroDeltas_MatchOnlyAny()
        {
            DeltaFilter[] directional = { new DeltaFilter("bonus", DeltaOperation.Add, 5, DeltaDirection.Increase) };
            DeltaFilter[] any = { new DeltaFilter("bonus", DeltaOperation.Add, 5) };

            Assert.AreEqual(0, DeltaPipeline.Apply(0, 0, directional).Filtered);
            Assert.AreEqual(5, DeltaPipeline.Apply(0, 0, any).Filtered);
        }

        [Test]
        public void Scale_RoundsTowardZeroForIntegralTypes()
        {
            DeltaFilter[] halve = { new DeltaFilter("halve", DeltaOperation.Scale, 0.5) };

            Assert.AreEqual(-7, DeltaPipeline.Apply(100, -15, halve).Filtered);
            Assert.AreEqual(7, DeltaPipeline.Apply(100, 15, halve).Filtered);
        }

        [Test]
        public void Filters_RunByPriorityThenGivenOrder()
        {
            DeltaFilter[] filters =
            {
                new DeltaFilter("double", DeltaOperation.Scale, 2, Priority: 5),
                new DeltaFilter("plus-one", DeltaOperation.Add, 1, Priority: 0),
            };

            // (10 + 1) * 2
            Assert.AreEqual(22, DeltaPipeline.Apply(0, 10, filters).Filtered);
        }

        [Test]
        public void Filters_ThatLeaveTheDeltaUnchangedDoNotFire()
        {
            DeltaFilter[] filters = { new DeltaFilter("cap", DeltaOperation.CapMagnitude, 50) };

            Assert.IsEmpty(DeltaPipeline.Apply(0, 20, filters).FiredFilters);
        }

        [Test]
        public void Apply_RejectsDuplicateIds()
        {
            DeltaFilter[] filters =
            {
                new DeltaFilter("same", DeltaOperation.Block),
                new DeltaFilter("same", DeltaOperation.Block),
            };

            Assert.Throws<ArgumentException>(() => DeltaPipeline.Apply(0, 1, filters));
        }

        [Test]
        public void DeltaFilter_ValidatesItsData()
        {
            Assert.Throws<ArgumentException>(() => _ = new DeltaFilter("", DeltaOperation.Block));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DeltaFilter("a", DeltaOperation.CapMagnitude, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DeltaFilter("a", DeltaOperation.Scale, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DeltaFilter("a", DeltaOperation.Scale, 1, (DeltaDirection)42));
        }
    }
}
