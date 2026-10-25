using System;
using System.Collections.Generic;
using System.Linq;
using BrunoCPF.Modifiable.Common.Math;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Pure, closure-free evaluation of a set of <see cref="Modifier{TSource}"/>s over a base value.
    /// Modifiers run in ascending <see cref="Modifier{TSource}.Priority"/>; equal priorities keep the
    /// order they were given in. Stack groups are resolved first, so a suppressed modifier never applies.
    /// Modifiers need no identity: two equal modifiers simply both apply.
    /// </summary>
    public static class ModifierStack
    {
        /// <summary>
        /// Returns the result of applying <paramref name="modifiers"/> to <paramref name="baseValue"/>.
        /// </summary>
        /// <param name="baseValue">The unmodified value.</param>
        /// <param name="modifiers">The modifiers.</param>
        /// <param name="math">Math for <typeparamref name="TValue"/>; defaults to the built-in implementation.</param>
        /// <param name="stacking">Policy per stack group; groups default to <see cref="StackPolicy.Stack"/>.</param>
        /// <param name="operations">Registry for <see cref="ModifierOperation.Custom"/> modifiers.</param>
        public static TValue Evaluate<TValue, TSource>(
            TValue baseValue,
            IEnumerable<Modifier<TSource>> modifiers,
            IScalableValueMath<TValue>? math = null,
            Func<string, StackPolicy>? stacking = null,
            CustomOperations? operations = null)
            where TValue : struct =>
            Run(baseValue, modifiers, math, stacking, operations, steps: null);

        /// <summary>
        /// Evaluates like <see cref="Evaluate{TValue, TSource}"/> and also returns every step, including
        /// modifiers suppressed by their stack group, with their sources.
        /// </summary>
        public static ModifierBreakdown<TValue, TSource> Explain<TValue, TSource>(
            TValue baseValue,
            IEnumerable<Modifier<TSource>> modifiers,
            IScalableValueMath<TValue>? math = null,
            Func<string, StackPolicy>? stacking = null,
            CustomOperations? operations = null)
            where TValue : struct
        {
            var steps = new List<ModifierStep<TValue, TSource>>();
            TValue result = Run(baseValue, modifiers, math, stacking, operations, steps);
            return new ModifierBreakdown<TValue, TSource>(baseValue, result, steps);
        }

        internal static IScalableValueMath<TValue> ResolveMath<TValue>(IScalableValueMath<TValue>? math)
            where TValue : struct =>
            math
            ?? ValueMath.GetScalableValueMath<TValue>()
            ?? throw new InvalidOperationException(
                $"No {nameof(IScalableValueMath<TValue>)} is registered for {typeof(TValue).Name}; pass one explicitly.");

        /// <summary>
        /// Marks which entries their stack group's policy excludes. <paramref name="ordered"/> must already
        /// be in evaluation order, which decides ties; null entries (function-based modifiers) are never
        /// suppressed.
        /// </summary>
        internal static bool[] FindSuppressed<TSource>(
            IReadOnlyList<Modifier<TSource>?> ordered,
            Func<string, StackPolicy>? stacking)
        {
            var suppressed = new bool[ordered.Count];
            if (stacking is null)
            {
                return suppressed;
            }

            var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int index = 0; index < ordered.Count; index++)
            {
                if (ordered[index]?.StackGroup is { } group)
                {
                    if (!groups.TryGetValue(group, out List<int>? members))
                    {
                        members = new List<int>();
                        groups.Add(group, members);
                    }

                    members.Add(index);
                }
            }

            foreach (KeyValuePair<string, List<int>> group in groups)
            {
                StackPolicy policy = stacking(group.Key);
                if (policy == StackPolicy.Stack)
                {
                    continue;
                }

                int kept = -1;
                foreach (int index in group.Value)
                {
                    double value = ordered[index]!.Value;
                    bool better = kept < 0
                        || (policy == StackPolicy.HighestOnly && value > ordered[kept]!.Value)
                        || (policy == StackPolicy.LowestOnly && value < ordered[kept]!.Value);
                    if (better)
                    {
                        kept = index;
                    }
                }

                foreach (int index in group.Value)
                {
                    suppressed[index] = index != kept;
                }
            }

            return suppressed;
        }

        private static TValue Run<TValue, TSource>(
            TValue baseValue,
            IEnumerable<Modifier<TSource>> modifiers,
            IScalableValueMath<TValue>? math,
            Func<string, StackPolicy>? stacking,
            CustomOperations? operations,
            List<ModifierStep<TValue, TSource>>? steps)
            where TValue : struct
        {
            if (modifiers is null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            IScalableValueMath<TValue> resolvedMath = ResolveMath(math);
            Modifier<TSource>[] ordered = Order(modifiers);
            bool[] suppressed = FindSuppressed<TSource>(ordered, stacking);

            TValue value = baseValue;
            for (int index = 0; index < ordered.Length; index++)
            {
                Modifier<TSource> modifier = ordered[index];
                TValue before = value;
                if (!suppressed[index])
                {
                    value = ModifierMath.Apply(value, modifier, resolvedMath, operations);
                }

                steps?.Add(new ModifierStep<TValue, TSource>(
                    modifier.Priority, before, value, suppressed[index], modifier.Source, modifier));
            }

            return value;
        }

        private static Modifier<TSource>[] Order<TSource>(IEnumerable<Modifier<TSource>> modifiers)
        {
            Modifier<TSource>[] list = modifiers.ToArray();
            if (Array.IndexOf(list, null) >= 0)
            {
                throw new ArgumentException("Modifiers cannot contain null.", nameof(modifiers));
            }

            // OrderBy is stable, so equal priorities keep the given order.
            return list.OrderBy(static modifier => modifier.Priority).ToArray();
        }
    }
}
