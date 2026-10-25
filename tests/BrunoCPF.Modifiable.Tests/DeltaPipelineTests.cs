using System;
using System.Linq;
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
            DeltaFilter<string>[] filters =
            {
                new DeltaFilter<string>(DeltaOperation.Scale, 0.5, DeltaDirection.Decrease, Source: "guard"),
                new DeltaFilter<string>(DeltaOperation.CapMagnitude, 30, Priority: 1, Source: "cap"),
            };

            DeltaResult<int, string> result = DeltaPipeline.Apply(40, -100, filters, new ValueBounds<int>(0, 100));

            Assert.AreEqual(40, result.Before);
            Assert.AreEqual(-100, result.Requested);
            Assert.AreEqual(-30, result.Filtered);
            Assert.AreEqual(10, result.After);
            Assert.AreEqual(-30, result.Applied);
            CollectionAssert.AreEqual(new[] { "guard", "cap" }, result.FiredFilters.Select(step => step.Source).ToArray());
        }

        [Test]
        public void Apply_ReportsTheChangeActuallyMadeAfterBounds()
        {
            DeltaResult<int, string> result = DeltaPipeline.Apply(10, -25, Array.Empty<DeltaFilter<string>>(), new ValueBounds<int>(0, 100));

            Assert.AreEqual(-25, result.Filtered);
            Assert.AreEqual(0, result.After);
            Assert.AreEqual(-10, result.Applied);
            Assert.IsEmpty(result.FiredFilters);
        }

        [Test]
        public void Apply_WithoutBoundsDoesNotClamp()
        {
            DeltaResult<int, string> result = DeltaPipeline.Apply(10, -25, Array.Empty<DeltaFilter<string>>());

            Assert.AreEqual(-15, result.After);
        }

        [Test]
        public void Filters_OnlyApplyToMatchingDirections()
        {
            DeltaFilter<string>[] filters = { new DeltaFilter<string>(DeltaOperation.Block, AppliesTo: DeltaDirection.Increase, Source: "heal-block") };

            DeltaResult<int, string> heal = DeltaPipeline.Apply(50, 20, filters);
            DeltaResult<int, string> damage = DeltaPipeline.Apply(50, -20, filters);

            Assert.AreEqual(50, heal.After);
            CollectionAssert.AreEqual(new[] { "heal-block" }, heal.FiredFilters.Select(step => step.Source).ToArray());
            Assert.AreEqual(30, damage.After);
            Assert.IsEmpty(damage.FiredFilters);
        }

        [Test]
        public void ZeroDeltas_MatchOnlyAny()
        {
            DeltaFilter<string>[] directional = { new DeltaFilter<string>(DeltaOperation.Add, 5, DeltaDirection.Increase, Source: "bonus") };
            DeltaFilter<string>[] any = { new DeltaFilter<string>(DeltaOperation.Add, 5, Source: "bonus") };

            Assert.AreEqual(0, DeltaPipeline.Apply(0, 0, directional).Filtered);
            Assert.AreEqual(5, DeltaPipeline.Apply(0, 0, any).Filtered);
        }

        [Test]
        public void Scale_RoundsTowardZeroForIntegralTypes()
        {
            DeltaFilter<string>[] halve = { new DeltaFilter<string>(DeltaOperation.Scale, 0.5, Source: "halve") };

            Assert.AreEqual(-7, DeltaPipeline.Apply(100, -15, halve).Filtered);
            Assert.AreEqual(7, DeltaPipeline.Apply(100, 15, halve).Filtered);
        }

        [Test]
        public void Filters_RunByPriorityThenGivenOrder()
        {
            DeltaFilter<string>[] filters =
            {
                new DeltaFilter<string>(DeltaOperation.Scale, 2, Priority: 5, Source: "double"),
                new DeltaFilter<string>(DeltaOperation.Add, 1, Priority: 0, Source: "plus-one"),
            };

            // (10 + 1) * 2
            Assert.AreEqual(22, DeltaPipeline.Apply(0, 10, filters).Filtered);
        }

        [Test]
        public void Filters_ThatLeaveTheDeltaUnchangedDoNotFire()
        {
            DeltaFilter<string>[] filters = { new DeltaFilter<string>(DeltaOperation.CapMagnitude, 50, Source: "cap") };

            Assert.IsEmpty(DeltaPipeline.Apply(0, 20, filters).FiredFilters);
        }

        [Test]
        public void Filters_NeedNoIdentity_EqualFiltersBothApply()
        {
            var halve = new DeltaFilter<string>(DeltaOperation.Scale, 0.5, Source: "guard");

            DeltaResult<int, string> result = DeltaPipeline.Apply(100, -40, new[] { halve, halve });

            Assert.AreEqual(-10, result.Filtered);
            Assert.AreEqual(2, result.FiredFilters.Count);
        }

        [Test]
        public void FiredFilters_ReportEachStepWithItsSourceAndData()
        {
            var guard = new DeltaFilter<string>(DeltaOperation.Scale, 0.5, Priority: 3, Source: "guard");

            DeltaStep<int, string> step = DeltaPipeline.Apply(100, -40, new[] { guard }).FiredFilters.Single();

            Assert.AreEqual((3, -40, -20, "guard"), (step.Priority, step.Before, step.After, step.Source));
            Assert.AreSame(guard, step.Data);
        }

        [Test]
        public void Filters_WithoutASourceStillReport()
        {
            DeltaFilter<string>[] filters = { new DeltaFilter<string>(DeltaOperation.Block) };

            DeltaStep<int, string> step = DeltaPipeline.Apply(10, 5, filters).FiredFilters.Single();

            Assert.IsNull(step.Source);
        }

        [Test]
        public void With_WorksOnDeltaFiltersAndCrossMemberRulesAreCheckedWhenApplied()
        {
            var cap = new DeltaFilter<string>(DeltaOperation.CapMagnitude, 10, Source: "cap");

            DeltaFilter<string> wider = cap with { Value = 20, AppliesTo = DeltaDirection.Decrease };
            DeltaFilter<string> negative = cap with { Value = -5 };

            Assert.AreEqual(-20, DeltaPipeline.Apply(0, -50, new[] { wider }).Filtered);
            Assert.Throws<ArgumentException>(() => DeltaPipeline.Apply(0, -50, new[] { negative }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = cap with { AppliesTo = (DeltaDirection)9 });
        }

        [Test]
        public void DeltaFilter_ValidatesItsData()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DeltaFilter<string>(DeltaOperation.CapMagnitude, -1, Source: "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DeltaFilter<string>(DeltaOperation.Scale, double.PositiveInfinity, Source: "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DeltaFilter<string>(DeltaOperation.Scale, 1, (DeltaDirection)42, Source: "a"));
        }
    }
}
