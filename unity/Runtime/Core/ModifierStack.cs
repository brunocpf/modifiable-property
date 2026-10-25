using System;
using System.Collections.Generic;
using System.Linq;
using BrunoCPF.Modifiable.Common.Math;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Pure, closure-free evaluation of a set of <see cref="Modifier"/>s over a base value. Modifiers run
    /// in ascending <see cref="Modifier.Priority"/>; equal priorities keep the order they were given in.
    /// Stack groups are resolved first, so a suppressed modifier never applies.
    /// </summary>
    public static class ModifierStack
    {
        /// <summary>
        /// Returns the result of applying <paramref name="modifiers"/> to <paramref name="baseValue"/>.
        /// </summary>
        /// <param name="baseValue">The unmodified value.</param>
        /// <param name="modifiers">The modifiers; ids must be unique.</param>
        /// <param name="math">Math for <typeparamref name="TValue"/>; defaults to the built-in implementation.</param>
        /// <param name="stacking">Policy per stack group; groups default to <see cref="StackPolicy.Stack"/>.</param>
        public static TValue Evaluate<TValue>(
            TValue baseValue,
            IEnumerable<Modifier> modifiers,
            IScalableValueMath<TValue>? math = null,
            Func<string, StackPolicy>? stacking = null)
            where TValue : struct =>
            Run(baseValue, modifiers, math, stacking, steps: null);

        /// <summary>
        /// Evaluates like <see cref="Evaluate{TValue}"/> and also returns every step, including
        /// modifiers suppressed by their stack group.
        /// </summary>
        public static ModifierBreakdown<TValue> Explain<TValue>(
            TValue baseValue,
            IEnumerable<Modifier> modifiers,
            IScalableValueMath<TValue>? math = null,
            Func<string, StackPolicy>? stacking = null)
            where TValue : struct
        {
            var steps = new List<ModifierStep<TValue>>();
            TValue result = Run(baseValue, modifiers, math, stacking, steps);
            return new ModifierBreakdown<TValue>(baseValue, result, steps);
        }

        internal static IScalableValueMath<TValue> ResolveMath<TValue>(IScalableValueMath<TValue>? math)
            where TValue : struct =>
            math
            ?? ValueMath.GetScalableValueMath<TValue>()
            ?? throw new InvalidOperationException(
                $"No {nameof(IScalableValueMath<TValue>)} is registered for {typeof(TValue).Name}; pass one explicitly.");

        /// <summary>
        /// Returns the ids of modifiers excluded by their stack group's policy. <paramref name="ordered"/>
        /// must already be in evaluation order, which decides ties.
        /// </summary>
        internal static HashSet<string> FindSuppressed(
            IEnumerable<Modifier> ordered,
            Func<string, StackPolicy>? stacking)
        {
            var suppressed = new HashSet<string>(StringComparer.Ordinal);
            if (stacking is null)
            {
                return suppressed;
            }

            foreach (IGrouping<string, Modifier> group in ordered
                         .Where(static modifier => modifier.StackGroup is not null)
                         .GroupBy(static modifier => modifier.StackGroup!, StringComparer.Ordinal))
            {
                StackPolicy policy = stacking(group.Key);
                if (policy == StackPolicy.Stack)
                {
                    continue;
                }

                Modifier? kept = null;
                foreach (Modifier candidate in group)
                {
                    bool better = kept is null
                        || (policy == StackPolicy.HighestOnly && candidate.Value > kept.Value)
                        || (policy == StackPolicy.LowestOnly && candidate.Value < kept.Value);
                    if (better)
                    {
                        kept = candidate;
                    }
                }

                foreach (Modifier candidate in group)
                {
                    if (!ReferenceEquals(candidate, kept))
                    {
                        suppressed.Add(candidate.Id);
                    }
                }
            }

            return suppressed;
        }

        private static TValue Run<TValue>(
            TValue baseValue,
            IEnumerable<Modifier> modifiers,
            IScalableValueMath<TValue>? math,
            Func<string, StackPolicy>? stacking,
            List<ModifierStep<TValue>>? steps)
            where TValue : struct
        {
            if (modifiers is null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            IScalableValueMath<TValue> resolvedMath = ResolveMath(math);
            Modifier[] ordered = Order(modifiers);
            HashSet<string> suppressed = FindSuppressed(ordered, stacking);

            TValue value = baseValue;
            foreach (Modifier modifier in ordered)
            {
                TValue before = value;
                bool isSuppressed = suppressed.Contains(modifier.Id);
                if (!isSuppressed)
                {
                    value = ModifierMath.Apply(value, modifier, resolvedMath);
                }

                steps?.Add(new ModifierStep<TValue>(modifier.Id, modifier.Priority, before, value, isSuppressed, modifier));
            }

            return value;
        }

        private static Modifier[] Order(IEnumerable<Modifier> modifiers)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<Modifier>();
            foreach (Modifier modifier in modifiers)
            {
                if (modifier is null)
                {
                    throw new ArgumentException("Modifiers cannot contain null.", nameof(modifiers));
                }

                if (!seen.Add(modifier.Id))
                {
                    throw new ArgumentException($"Duplicate modifier id '{modifier.Id}'.", nameof(modifiers));
                }

                list.Add(modifier);
            }

            // OrderBy is stable, so equal priorities keep the given order.
            return list.OrderBy(static modifier => modifier.Priority).ToArray();
        }
    }
}
