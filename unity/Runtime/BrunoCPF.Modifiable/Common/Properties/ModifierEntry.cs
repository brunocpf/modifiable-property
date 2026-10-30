using System;
using BrunoCPF.Modifiable.Core;

namespace BrunoCPF.Modifiable.Common.Properties
{
    /// <summary>
    /// One modifier installed on a <see cref="ModifiableProperty{TValue, TContext, TSource}"/>: either a
    /// data <see cref="Modifier{TSource}"/> or a function. Entries have reference identity; the handle
    /// returned when it was pushed removes exactly this entry.
    /// </summary>
    public sealed class ModifierEntry<TValue, TSource> where TValue : struct
    {
        internal ModifierEntry(Modifier<TSource> data)
        {
            Data = data;
            Priority = data.Priority;
            Source = data.Source;
        }

        internal ModifierEntry(Func<TValue, TValue> function, int priority, TSource? source)
        {
            Function = function;
            Priority = priority;
            Source = source;
        }

        /// <summary>Lower numbers run earlier; equal priorities keep insertion order.</summary>
        public int Priority { get; }

        /// <summary>Where the modifier came from, if it has a source.</summary>
        public TSource? Source { get; }

        /// <summary>The data modifier, or null for a function-based entry.</summary>
        public Modifier<TSource>? Data { get; }

        /// <summary>The function, or null for a data entry.</summary>
        public Func<TValue, TValue>? Function { get; }
    }
}
