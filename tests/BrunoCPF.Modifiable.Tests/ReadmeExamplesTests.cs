using System;
using BrunoCPF.Modifiable.Common.Math;
using BrunoCPF.Modifiable.Common.Properties;
using NUnit.Framework;

namespace BrunoCPF.Modifiable.Tests
{
    // Mirrors README examples that are easy to break, so they keep compiling and behaving as documented.
    public class ReadmeExamplesTests
    {
#pragma warning disable CA1036 // Kept as minimal as the README example; comparison operators aren't needed.
        public readonly struct Mana : IComparable
        {
            public Mana(int value) => Value = value;
            public int Value { get; }
            public int CompareTo(object? obj) => obj is Mana mana ? Value.CompareTo(mana.Value) : 1;
        }
#pragma warning restore CA1036

        public class ManaMath : IValueMath<Mana>
        {
            public Mana Add(Mana a, Mana b) => new(a.Value + b.Value);
            public Mana Subtract(Mana a, Mana b) => new(a.Value - b.Value);
        }

        [Test]
        public void CustomMath_ManaExample()
        {
            using var mana = new ModifiableProperty<Mana, object>(new Mana(10), valueMath: new ManaMath());
            using var bounded = new ModifiableProperty<Mana, object>(
                new Mana(10), new ValueBounds<Mana>(new Mana(0), new Mana(99)), new ManaMath());

            mana.AddDelta(new Mana(5));
            bounded.AddDelta(new Mana(500));

            Assert.AreEqual(15, mana.CurrentValue.Value);
            Assert.AreEqual(99, bounded.CurrentValue.Value);
        }

        [Test]
        public void Bounds_ExampleWithOnlyAMinimum()
        {
            using var attack = new ModifiableProperty<int, object>(10, min: 1);

            attack.AddDelta(+1000);

            Assert.AreEqual(1010, attack.CurrentValue);
        }
    }
}
