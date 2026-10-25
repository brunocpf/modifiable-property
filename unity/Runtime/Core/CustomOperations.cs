using System;
using System.Collections.Generic;

namespace BrunoCPF.Modifiable.Core
{
    /// <summary>
    /// Registry of named custom operations. <see cref="ModifierOperation.Custom"/> modifiers and
    /// <see cref="DeltaOperation.Custom"/> filters refer to an operation by name, so they stay plain,
    /// saveable data while the logic can be anything (soft caps, curves, lookup tables).
    /// </summary>
    /// <remarks>
    /// Register every operation once at startup and pass the registry to the evaluators (or set
    /// <c>ModifiableProperty.Operations</c>). Names are case-sensitive and can't be registered twice
    /// or removed, so a name always means the same operation for the registry's lifetime.
    /// </remarks>
    public sealed class CustomOperations
    {
        private readonly Dictionary<string, CustomModifierOperation> _modifierOperations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CustomDeltaOperation> _deltaOperations = new(StringComparer.Ordinal);

        /// <summary>
        /// Registers a read-time operation for <see cref="ModifierOperation.Custom"/> modifiers.
        /// </summary>
        /// <returns>This registry, for chaining.</returns>
        /// <exception cref="ArgumentException">The name is blank or already registered.</exception>
        public CustomOperations AddModifierOperation(string name, CustomModifierOperation operation)
        {
            Add(_modifierOperations, name, operation, "modifier");
            return this;
        }

        /// <summary>
        /// Registers a change-time operation for <see cref="DeltaOperation.Custom"/> filters.
        /// </summary>
        /// <returns>This registry, for chaining.</returns>
        /// <exception cref="ArgumentException">The name is blank or already registered.</exception>
        public CustomOperations AddDeltaOperation(string name, CustomDeltaOperation operation)
        {
            Add(_deltaOperations, name, operation, "delta");
            return this;
        }

        /// <summary>
        /// Whether a modifier operation with this name is registered.
        /// </summary>
        public bool HasModifierOperation(string name) => name is not null && _modifierOperations.ContainsKey(name);

        /// <summary>
        /// Whether a delta operation with this name is registered.
        /// </summary>
        public bool HasDeltaOperation(string name) => name is not null && _deltaOperations.ContainsKey(name);

        internal static double ApplyModifier(CustomOperations? operations, string name, double value, double operand)
        {
            if (operations is null || !operations._modifierOperations.TryGetValue(name, out CustomModifierOperation? operation))
            {
                throw Missing("modifier", name, operations is null);
            }

            return RequireFinite(operation(value, operand), "modifier", name);
        }

        internal static double ApplyDelta(CustomOperations? operations, string name, double delta, double operand)
        {
            if (operations is null || !operations._deltaOperations.TryGetValue(name, out CustomDeltaOperation? operation))
            {
                throw Missing("delta", name, operations is null);
            }

            return RequireFinite(operation(delta, operand), "delta", name);
        }

        private static void Add<TOperation>(
            Dictionary<string, TOperation> operations,
            string name,
            TOperation operation,
            string kind)
            where TOperation : Delegate
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"Custom {kind} operation name cannot be blank.", nameof(name));
            }

            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (operations.ContainsKey(name))
            {
                throw new ArgumentException($"Custom {kind} operation '{name}' is already registered.", nameof(name));
            }

            operations.Add(name, operation);
        }

        private static InvalidOperationException Missing(string kind, string name, bool noRegistry) =>
            new InvalidOperationException(noRegistry
                ? $"Custom {kind} operation '{name}' needs a {nameof(CustomOperations)} registry; none was supplied."
                : $"Custom {kind} operation '{name}' is not registered.");

        private static double RequireFinite(double result, string kind, string name) =>
            double.IsFinite(result)
                ? result
                : throw new InvalidOperationException($"Custom {kind} operation '{name}' returned {result}; results must be finite.");
    }
}
