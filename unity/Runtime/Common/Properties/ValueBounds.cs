using System;

namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// Optional inclusive minimum and maximum for a value. A side that was not supplied is unbounded.
    /// </summary>
    public sealed record ValueBounds<TValue> where TValue : IComparable
    {
        /// <summary>
        /// Bounds that never clamp.
        /// </summary>
        public static ValueBounds<TValue> Unbounded { get; } = new ValueBounds<TValue>();

        /// <summary>
        /// Creates unbounded bounds; equivalent to <see cref="Unbounded"/>.
        /// </summary>
        public ValueBounds()
            : this(false, default!, false, default!)
        { }

        /// <summary>
        /// Creates bounds with an inclusive minimum and no maximum. A null reference-type
        /// <paramref name="min"/> leaves the minimum unbounded.
        /// </summary>
        public ValueBounds(TValue min)
            : this(min is not null, min, false, default!)
        { }

        /// <summary>
        /// Creates bounds with an inclusive minimum and maximum. A null reference-type bound leaves
        /// that side unbounded.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="min"/> is greater than <paramref name="max"/>.</exception>
        public ValueBounds(TValue min, TValue max)
            : this(min is not null, min, max is not null, max)
        { }

        internal ValueBounds(bool hasMin, TValue? min, bool hasMax, TValue? max)
        {
            if (hasMin && hasMax && min!.CompareTo(max) > 0)
            {
                throw new ArgumentException($"Minimum {min} is greater than maximum {max}.", nameof(min));
            }

            HasMin = hasMin;
            HasMax = hasMax;
            Min = hasMin ? min : default;
            Max = hasMax ? max : default;
        }

        /// <summary>
        /// Creates bounds with an inclusive minimum and no maximum.
        /// </summary>
        public static ValueBounds<TValue> AtLeast(TValue min) => new ValueBounds<TValue>(true, min, false, default!);

        /// <summary>
        /// Creates bounds with an inclusive maximum and no minimum.
        /// </summary>
        public static ValueBounds<TValue> AtMost(TValue max) => new ValueBounds<TValue>(false, default!, true, max);

        /// <summary>
        /// Whether a minimum applies.
        /// </summary>
        public bool HasMin { get; }

        /// <summary>
        /// Whether a maximum applies.
        /// </summary>
        public bool HasMax { get; }

        /// <summary>
        /// The inclusive minimum; meaningful only when <see cref="HasMin"/> is true.
        /// </summary>
        public TValue? Min { get; }

        /// <summary>
        /// The inclusive maximum; meaningful only when <see cref="HasMax"/> is true.
        /// </summary>
        public TValue? Max { get; }

        public TValue Clamp(TValue value)
        {
            if (HasMin && value.CompareTo(Min) < 0)
            {
                return Min!;
            }

            if (HasMax && value.CompareTo(Max) > 0)
            {
                return Max!;
            }

            return value;
        }

        public bool Contains(TValue value)
        {
            if (HasMin && value.CompareTo(Min) < 0)
            {
                return false;
            }

            if (HasMax && value.CompareTo(Max) > 0)
            {
                return false;
            }

            return true;
        }
    }
}
