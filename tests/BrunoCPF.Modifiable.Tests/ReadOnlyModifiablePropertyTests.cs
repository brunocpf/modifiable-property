using System;
using BrunoCPF.Modifiable.Common.Properties;
using NUnit.Framework;
using R3;

namespace BrunoCPF.Modifiable.Tests
{
    public class ReadOnlyModifiablePropertyTests
    {
        [Test]
        public void AsReadOnly_ReflectsSourceCurrentValueAndBase()
        {
            using ModifiableProperty<int, object> hp = new(100, new ValueBounds<int>(0, int.MaxValue));
            IReadOnlyModifiableProperty<int, object> view = hp.AsReadOnly();

            Assert.AreEqual(100, view.CurrentValue);

            hp.AddDelta(-30);

            Assert.AreEqual(70, view.CurrentValue);
            Assert.AreEqual(70, view.Base.CurrentValue);
        }

        [Test]
        public void AsReadOnly_CannotBeCastBackToWritableProperty()
        {
            using ModifiableProperty<int, object> hp = new(100, new ValueBounds<int>(0, int.MaxValue));
            IReadOnlyModifiableProperty<int, object> view = hp.AsReadOnly();

            // The whole point of the view: there is no downcast that regains the writable API.
            Assert.That(view, Is.Not.InstanceOf<ModifiableProperty<int, object>>());
        }

        [Test]
        public void AsReadOnly_ValueStreamEmitsChanges()
        {
            using ModifiableProperty<int, object> hp = new(100, new ValueBounds<int>(0, int.MaxValue));
            IReadOnlyModifiableProperty<int, object> view = hp.AsReadOnly();

            int latest = 0;
            using IDisposable sub = view.ToObservable().Subscribe(value => latest = value);

            hp.AddDelta(-25);

            Assert.AreEqual(75, latest);
        }

        [Test]
        public void AsReadOnly_ProcessedDeltasForwardsAppliedDeltas()
        {
            using ModifiableProperty<int, object> hp = new(100, new ValueBounds<int>(0, int.MaxValue));
            IReadOnlyModifiableProperty<int, object> view = hp.AsReadOnly();

            int lastDelta = 0;
            using IDisposable sub = view.ProcessedDeltas.Subscribe(delta => lastDelta = delta.Delta);

            hp.AddDelta(-25);

            Assert.AreEqual(-25, lastDelta);
        }
    }
}
