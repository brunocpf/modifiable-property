using System;
using System.Collections.Generic;
using System.Linq;
using BrunoCPF.Modifiable.Common.Math;
using BrunoCPF.Modifiable.Common.Properties;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Pure, closure-free processing of a delta: filters in ascending priority (equal priorities keep
    /// their given order), then bounds.
    /// </summary>
    public static class DeltaPipeline
    {
        /// <summary>
        /// Applies <paramref name="delta"/> to <paramref name="current"/> through <paramref name="filters"/>
        /// and <paramref name="bounds"/>.
        /// </summary>
        /// <param name="current">The stored value before the change.</param>
        /// <param name="delta">The requested change.</param>
        /// <param name="filters">The filters; ids must be unique.</param>
        /// <param name="bounds">Optional bounds for the resulting value.</param>
        /// <param name="math">Math for <typeparamref name="TValue"/>; defaults to the built-in implementation.</param>
        /// <param name="operations">Registry for <see cref="DeltaOperation.Custom"/> filters.</param>
        public static DeltaResult<TValue> Apply<TValue>(
            TValue current,
            TValue delta,
            IEnumerable<DeltaFilter> filters,
            ValueBounds<TValue>? bounds = null,
            IScalableValueMath<TValue>? math = null,
            CustomOperations? operations = null)
            where TValue : struct, IComparable
        {
            if (filters is null)
            {
                throw new ArgumentNullException(nameof(filters));
            }

            IScalableValueMath<TValue> resolvedMath = ModifierStack.ResolveMath(math);
            var fired = new List<string>();
            TValue filtered = delta;
            foreach (DeltaFilter filter in Order(filters))
            {
                TValue next = ApplyFilter(filtered, filter, resolvedMath, operations);
                if (!EqualityComparer<TValue>.Default.Equals(next, filtered))
                {
                    fired.Add(filter.Id);
                }

                filtered = next;
            }

            TValue after = resolvedMath.Add(current, filtered);
            if (bounds is not null)
            {
                after = bounds.Clamp(after);
            }

            return new DeltaResult<TValue>(
                current,
                delta,
                filtered,
                after,
                resolvedMath.Subtract(after, current),
                fired);
        }

        /// <summary>
        /// Applies one filter to a delta. Filters whose <see cref="DeltaFilter.AppliesTo"/> does not
        /// match the delta's sign return it unchanged; zero deltas match only <see cref="DeltaDirection.Any"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// A custom filter's operation isn't registered, or it returned a non-finite result.
        /// </exception>
        public static TValue ApplyFilter<TValue>(
            TValue delta,
            DeltaFilter filter,
            IScalableValueMath<TValue> math,
            CustomOperations? operations = null)
            where TValue : struct
        {
            if (filter is null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            if (math is null)
            {
                throw new ArgumentNullException(nameof(math));
            }

            double value = math.ToDouble(delta);
            bool applies = filter.AppliesTo switch
            {
                DeltaDirection.Any => true,
                DeltaDirection.Increase => value > 0d,
                DeltaDirection.Decrease => value < 0d,
                _ => false,
            };
            if (!applies)
            {
                return delta;
            }

            double result = filter.Operation switch
            {
                DeltaOperation.Scale => value * filter.Value,
                DeltaOperation.Add => value + filter.Value,
                DeltaOperation.CapMagnitude => System.Math.Sign(value) * System.Math.Min(System.Math.Abs(value), filter.Value),
                DeltaOperation.Block => 0d,
                DeltaOperation.Custom => CustomOperations.ApplyDelta(operations, filter.CustomName!, value, filter.Value),
                _ => throw new ArgumentOutOfRangeException(nameof(filter), filter.Operation, null),
            };

            return math.FromDouble(result);
        }

        private static DeltaFilter[] Order(IEnumerable<DeltaFilter> filters)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<DeltaFilter>();
            foreach (DeltaFilter filter in filters)
            {
                if (filter is null)
                {
                    throw new ArgumentException("Filters cannot contain null.", nameof(filters));
                }

                if (!seen.Add(filter.Id))
                {
                    throw new ArgumentException($"Duplicate delta filter id '{filter.Id}'.", nameof(filters));
                }

                list.Add(filter);
            }

            return list.OrderBy(static filter => filter.Priority).ToArray();
        }
    }
}
