# `com.brunocpf.modifiable-property`

_A reactive, extensible stat & value-transformation pipeline for C#, Unity, and Godot (powered by R3)_

<p align="center">
  <img src="https://img.shields.io/badge/Unity-2021%2B-black?logo=unity" />
  <img src="https://img.shields.io/badge/Godot-4%20(.NET)-478cbf?logo=godotengine&logoColor=white" />
  <img src="https://img.shields.io/badge/.NET-netstandard2.1-512bd4?logo=dotnet&logoColor=white" />
  <img src="https://img.shields.io/nuget/v/BrunoCPF.Modifiable?logo=nuget&label=NuGet" />
  <img src="https://img.shields.io/badge/R3-Compatible-blue" />
  <img src="https://img.shields.io/badge/License-MIT-green" />
  <br>
  <img src="https://img.shields.io/github/stars/brunocpf/modifiable-property?style=social" />
</p>

---

## What Is This?

**ModifiableProperty** is a powerful, reactive wrapper around a value that supports:

-   **Deltas** — incremental changes (damage, healing, EXP gain, gold gain)
-   **Filters** — change-time transformations (EXP boosts, healing block, damage reduction)
-   **Bounds** — min/max constraints (HP ≥ 0, HP ≤ MaxHP)
-   **Modifiers** — view-time transformations (equipment, buffs, multipliers)
-   **Disposable Push/Pop Effects** — perfect for RPG buffs, items, equipment, temporary states
-   **Context-aware changes** — pass structured metadata with each delta
-   **Reactive observation** — subscribe to base, effective, or delta streams
-   **Custom numeric support** — define your own math for custom structs

It is ideal for:

-   RPG stats
-   Character attributes
-   Currency systems
-   Simulation values
-   Buff & debuff systems
-   Combat logic
-   Ability and item effects

---

## Installation

The library code is **engine-agnostic** (no `UnityEngine` / `Godot` references) and lives in
one place — `unity/Runtime`. Unity compiles it via the UPM `.asmdef`; the SDK project in `src/`
compiles the *same* files for Godot / plain .NET / NuGet. Edit once, both engines build it.

### Unity (UPM)

Add via **Package Manager → Add package from git URL**:

```
https://github.com/brunocpf/modifiable-property.git?path=unity
```

Or in `Packages/manifest.json`:

```json
{
    "dependencies": {
        "com.brunocpf.modifiable-property": "https://github.com/brunocpf/modifiable-property.git?path=unity"
    }
}
```

> The package lives in the `unity/` subfolder, hence the `?path=unity` suffix.

**R3 prerequisite (Unity):** install R3 core via [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)
(the `R3` NuGet package) plus the [`R3.Unity`](https://github.com/Cysharp/R3) package for the engine
integration, before adding this package.

### Godot 4 (.NET) / plain .NET

The library targets `netstandard2.1`, which Godot 4's .NET runtime and any modern .NET app
consume directly; R3 flows in transitively.

**Install from [NuGet.org](https://www.nuget.org/packages/BrunoCPF.Modifiable):**

```bash
dotnet add package BrunoCPF.Modifiable
```

**Or reference the source project** (for local iteration / debugging into the library):

```xml
<ItemGroup>
  <ProjectReference Include="path/to/modifiable-property/src/BrunoCPF.Modifiable/BrunoCPF.Modifiable.csproj" />
</ItemGroup>
```

> Outside Unity, never reference `R3.Unity` — the plain `R3` NuGet package (pulled in
> automatically by the library) is the one you want.

---

# Core Concept

A `ModifiableProperty` processes changes through **five ordered layers**:

```
Raw Deltas
    ↓
Filters (change-time logic)
    ↓
Bounds (min/max)
    ↓
Base Value (persistent)
    ↓
Modifiers (view-time logic)
    ↓
Effective Value (final)
```

Each layer solves a different category of gameplay logic:

| Layer          | Purpose                 | Examples                                     |
| -------------- | ----------------------- | -------------------------------------------- |
| **Deltas**     | Raw changes             | -10 HP, +100 EXP, +1 Level                   |
| **Filters**    | Modify or reject deltas | EXP boost, block healing, clamp HP by Max HP |
| **Bounds**     | Enforce range           | HP ≥ 0, ATK ≤ 999                            |
| **Base Value** | Accumulated state       | Real stored HP/EXP/etc                       |
| **Modifiers**  | View-time effects       | Equipment, buffs, debuffs                    |

---

# Quick Example

```csharp
var hp = new ModifiableProperty<int, object>(
    initialValue: 100,
    min: 0,
    max: 100
);

// Damage
hp.AddDelta(-30); // → 70

// Healing
hp.AddDelta(+20); // → 90

// Equipment (modifier)
var sword = hp.PushModifier(v => v + 10, source: "sword");

Debug.Log(hp.CurrentValue); // → 100

sword.Dispose(); // remove equipment
```

---

# API Overview

```csharp
public class ModifiableProperty<TValue, TContext, TSource>   // typed sources
public sealed class ModifiableProperty<TValue, TContext>     // sources are object
```

`TSource` is what an effect came from (a status, item, skill, …). It's optional, doesn't have to be
unique, and is what `RemoveModifiers(source)` / `RemoveFilters(source)` match on (by `Equals`).

### ✔ Add deltas

`AddDelta(delta)`
`AddDelta(ValueDelta<TValue, TContext>)`

### ✔ Add / remove temporary effects

`PushModifier(...)` / `PushFilter(...)` return an `IDisposable` handle that removes exactly that entry.
`RemoveModifiers(source)` / `RemoveFilters(source)` remove every entry from a source.
`ClearModifiers()` / `ClearFilters()` remove everything.

### ✔ Set base value

`SetBaseValue(newValue)`

### ✔ Observe values

-   `property.Subscribe(...)`
-   `property.Base.Subscribe(...)`
-   `property.ProcessedDeltas.Subscribe(...)`

### ✔ Encapsulation

```csharp
public interface IReadOnlyModifiableProperty<TValue, TContext>
```

-   Read-only access to ModifiableProperty (exposes value, base and delta streams, but not mutation methods).

Use:

```csharp
private ModifiableProperty<int, object> hp = new(...);
public IReadOnlyModifiableProperty<int, object> HP => hp;
```

---

# Filters (Change-Time Logic)

Filters modify _deltas_ before they affect the base value.

### EXP Boost

```csharp
exp.PushFilter(d => d with { Delta = (int)(d.Delta * 1.5f) }, source: "boost");
```

### Block Healing

```csharp
hp.PushFilter(d => d.Delta > 0 ? d with { Delta = 0 } : d, source: "no-healing");
```

### Minimum Damage Rule (“1 damage minimum”)

```csharp
hp.PushFilter(d => d.Delta < 0 && d.Delta > -1 ? new(-1, d.Context) : d, source: "min-dmg");
```

Filters run in **priority order** (the `priority` argument), lowest first. Filters with equal priority run in the order they were added.

---

# Modifiers (View-Time Logic)

Modifiers affect the **final value** after the base is computed.

### Equipment

```csharp
atk.PushModifier(v => v + 20, source: "sword");
```

### Buff

```csharp
atk.PushModifier(v => (int)(v * 1.2f), source: "atk-up");
```

### Debuff

```csharp
atk.PushModifier(v => (int)(v * 0.5f), source: "atk-down");
```

Modifiers also stack in **priority order** (the `priority` argument), lowest first. Modifiers with equal priority apply in the order they were added, so non-commutative modifiers (e.g. add vs. multiply) resolve deterministically.

In most cases, you want flat bonuses to have a lower priority than multipliers.

---

# Bounds

Bounds apply **after deltas are integrated**, not to modifiers.

Example:

```csharp
var hp = new ModifiableProperty<int, object>(50, min: 0, max: 100);
hp.AddDelta(-200); // → 0
hp.AddDelta(+999); // → 100
```

An omitted side is unbounded: `new ModifiableProperty<int, object>(10, min: 1)` has no maximum, and
`new ModifiableProperty<int, object>(10)` is not clamped at all. With an explicit `ValueBounds`, use
`new ValueBounds<int>(0, 100)`, `ValueBounds<int>.AtLeast(1)`, `ValueBounds<int>.AtMost(99)` or
`ValueBounds<int>.Unbounded`.

Use bounds to enforce static gameplay rules (e.g., HP can’t ever go below 0). For dynamic caps (e.g., HP ≤ MaxHP), use filters.

---

# Delta Contexts

You can optionally provide metadata for filtering logic:

```csharp
public record DamageContext(Battler Source) : IValueContext;

hp.AddDelta(-30, new DamageContext(attacker));
```

Filters and subscribers receive structured metadata and adjust their behavior accordingly.

```csharp
hp.PushFilter(d =>
{
    if (d.Context is DamageContext dc && dc.Source.HasStatus("armor_break"))
    {
        return d; // no reduction
    }

    return d.Delta < 0 ? d with { Delta = d.Delta + 10 } : d;
}, source: "shield");
```

---

# Custom Math (Non-Numeric Types)

By default, `ModifiableProperty` provides arithmetic for `int`, `long`, `float`, and `double`.

If your type doesn’t support numeric operators, supply custom math:

```csharp
public struct Mana { public int Value; }

public class ManaMath : IValueMath<Mana>
{
    public Mana Add(Mana a, Mana b) => new() { Value = a.Value + b.Value };
    public Mana Subtract(Mana a, Mana b) => new() { Value = a.Value - b.Value };
}
```

Use it like:

```csharp
var mana = new ModifiableProperty<Mana, object>(
    new Mana { Value = 10 },
    valueMath: new ManaMath()
);
```

This is particularly useful for enums with custom logic (e.g., elemental affinities).

If you don't provide custom math and use a non-numeric type, you might get unexpected behavior.

To use [data modifiers and filters](#pure-core-data-modifiers--filters) with a custom type, implement
`IScalableValueMath<T>` instead. It adds `ToDouble` / `FromDouble`, so scaling, overrides and caps can
be computed and converted back (rounding and saturating as you choose).

---

# Pure Core (Data Modifiers & Filters)

`BrunoCPF.Modifiable.Core` is a closure-free, non-reactive core. Modifiers and filters are plain
records instead of lambdas, so you can compare, copy, save and describe them, and the evaluators
report what each one did. It's built for simulations, saves, replays and AI lookahead, where state
has to be plain data. `ModifiableProperty` is built on top of it.

### Data modifiers

```csharp
using BrunoCPF.Modifiable.Core;

Modifier<string>[] modifiers =
{
    new Modifier<string>(ModifierOperation.Add, 10, Source: "sword"),
    new Modifier<string>(ModifierOperation.Multiply, 1.2, Priority: 1, Source: "boost"),
};

int strength = ModifierStack.Evaluate(40, modifiers); // (40 + 10) × 1.2 = 60
```

Operations: `Add`, `Multiply`, `Override`, `AtLeast`, `AtMost`. Modifiers run in ascending
`Priority`; equal priorities keep the order you pass them in. Modifiers have no identity: `Source`
(any type you like — a string, an enum, your own record) is optional, used only for attribution, and
two equal modifiers simply both apply.

### Stack groups

Modifiers that share a `StackGroup` combine according to a `StackPolicy`: `Stack` (default),
`HighestOnly` or `LowestOnly`.

```csharp
Modifier<string>[] boosts =
{
    new Modifier<string>(ModifierOperation.Multiply, 1.25, StackGroup: "boost.fire", Source: "ring"),
    new Modifier<string>(ModifierOperation.Multiply, 1.5, StackGroup: "boost.fire", Source: "skill"),
};

int damage = ModifierStack.Evaluate(100, boosts, stacking: _ => StackPolicy.HighestOnly); // 150
```

### Breakdowns

`Explain` returns every step, including modifiers suppressed by their group, for tooltips and
debugging:

```csharp
ModifierBreakdown<int, string> breakdown = ModifierStack.Explain(40, modifiers);
// Base 40 → sword 40→50 → boost 50→60 = 60   (each step carries its Source and Data)
```

### Data filters

```csharp
DeltaFilter<string>[] filters =
{
    new DeltaFilter<string>(DeltaOperation.Scale, 0.5, DeltaDirection.Decrease, Source: "guard"),
    new DeltaFilter<string>(DeltaOperation.CapMagnitude, 30, Priority: 1, Source: "damage-cap"),
};

DeltaResult<int, string> hit = DeltaPipeline.Apply(current: 40, delta: -100, filters, new ValueBounds<int>(0, 100));
// hit.Filtered == -30, hit.After == 10, hit.Applied == -30
// hit.FiredFilters: guard (-100 → -50), damage-cap (-50 → -30)
```

Operations: `Scale`, `Add`, `CapMagnitude`, `Block`. `DeltaDirection` limits a filter to increases,
decreases or both. `FiredFilters` lists each filter that actually changed the delta (its `Source`,
`Data`, and the delta before and after), which is what you need to show "Guard!" or log a trigger.

### Custom operations

When the built-in operations aren't enough (soft caps, curves, lookup tables), register a named
operation once and refer to it by name. The modifier or filter stays plain data that you can save,
compare and explain, and the breakdown shows the name.

```csharp
var operations = new CustomOperations()
    // above the threshold, only half of the excess counts
    .AddModifierOperation("soft-cap", (value, threshold) =>
        value <= threshold ? value : threshold + (value - threshold) / 2)
    // never drop more than the operand in one hit
    .AddDeltaOperation("endure", (delta, floor) => Math.Max(delta, floor));

var cap = new Modifier<string>(ModifierOperation.Custom, 999, CustomName: "soft-cap", Source: "str-cap");
int strength = ModifierStack.Evaluate(1200, new[] { cap }, operations: operations); // 1099

var endure = new DeltaFilter<string>(DeltaOperation.Custom, -99, CustomName: "endure", Source: "endure");
DeltaResult<int, string> hit = DeltaPipeline.Apply(100, -500, new[] { endure }, operations: operations); // After == 1
```

- Operations take `(value or delta, operand)` as `double` and return a `double`. The result is
  converted back like the built-in operations (integral types round per step) and must be finite.
- Names are case-sensitive and can't be registered twice or removed, so a saved name always means
  the same operation. Modifier and delta operations have separate namespaces.
- `CustomName` is required for `Custom` and not allowed with any other operation.
- An unregistered name, or a missing registry, throws `InvalidOperationException` when evaluated.

### Rounding

Values are combined in `double` and converted back after **every** step. `int` and `long` round
toward zero by default (`7.5 → 7`, `-7.5 → -7`) and saturate at their limits. Pick another mode with
`new IntValueMath(RoundingMode.Floor)` (also `Ceiling`, `Nearest`) and pass it as `math`. `long`
values above 2^53 lose precision when scaled.

### With `ModifiableProperty`

`ModifiableProperty` accepts data entries next to function-based ones, in one priority order. Use the
three-parameter form for typed sources.

```csharp
using var attack = new ModifiableProperty<int, object, string>(100, new ValueBounds<int>(0, 9999));

attack.PushModifier(new Modifier<string>(ModifierOperation.Multiply, 1.25, StackGroup: "boost", Source: "ring"));
attack.PushModifier(new Modifier<string>(ModifierOperation.Multiply, 1.5, StackGroup: "boost", Source: "skill"));
attack.Stacking = _ => StackPolicy.HighestOnly;  // 150

using var guard = attack.PushFilter(new DeltaFilter<string>(DeltaOperation.Scale, 0.5, DeltaDirection.Decrease, Source: "guard"));
attack.AppliedDeltas.Subscribe(result => Log(result.FiredFilters.Select(step => step.Source)));

ModifierBreakdown<int, string> breakdown = attack.Explain();
attack.RemoveModifiers("skill");                 // back to 125
```

`AppliedDeltas` emits a `DeltaResult` for every change, reporting function-based filters too. Data
entries need an `IScalableValueMath<T>`; the built-in `int`, `long`, `float` and `double` math
already implement it.

For custom operations, set `Operations` before adding custom entries:

```csharp
using var strength = new ModifiableProperty<int, object, string>(1200) { Operations = operations };
strength.PushModifier(new Modifier<string>(ModifierOperation.Custom, 999, CustomName: "soft-cap", Source: "str-cap"));
```

Adding a custom entry whose name isn't registered throws, and so does replacing `Operations` with a
registry that's missing a name in use. Evaluation itself never hits an unknown name.

---

# Integration Patterns

## Buffs

```csharp
IDisposable buff = atk.PushModifier(v => v + 10, source: tarukaja);
buff.Dispose();                 // removes this entry
atk.RemoveModifiers(tarukaja);  // or: removes everything the status added
```

## Equipment

```csharp
IDisposable equip = atk.PushModifier(v => v + 25, source: sword);
equip.Dispose(); // unequip
```

## Blocking Healing

```csharp
var block = hp.PushFilter(d => d.Delta > 0 ? d with { Delta = 0 } : d, source: "block-healing");

block.Dispose();
```

---

# Observability

### Effective value

```csharp
hp.Subscribe(v => Debug.Log($"HP → {v}"));
```

### Base value

```csharp
hp.Base.Subscribe(v => Debug.Log($"Base HP → {v}"));
```

### Processed deltas

Use this to trigger effects on changes. Useful for damage/healing reactions.

```csharp
hp.ProcessedDeltas.Subscribe(d =>
    Debug.Log($"Delta {d.Delta}, context={d.Context}")
);
```

---

# Best Practices

### ✔ Use filters for change-time logic

(EXP boosts, reducing damage, blocking healing)

### ✔ Use modifiers for view-time logic

(Equipment, buffs, transformations)

### ✔ Tag effects with their source

Entries need no ids. Pass the status, item or skill instance as `source` so you can remove
everything it added with one call and see it in breakdowns. Two identical buffs from two sources just
stack.

### ✔ Use disposables for lifetime

Temporary effects naturally tie into gameplay duration. Use `IDisposable` references to manage lifetimes (e.g., buffs, equipment).

### ✔ Keep heavy logic outside subscriptions

Keep subscriptions observational, not mutative.

---

# FAQ

### **Q: Can modifiers cause infinite loops?**

No. Value flow is strictly one-directional:

```
deltas → base → modifiers → effective
```

Still, maintain discipline in filters/modifiers to avoid unintended side effects.

---

# RPG Example

```csharp
public interface ICtx { }
public record AttackCtx(Battler Source) : ICtx;
public record HealCtx(Battler Source) : ICtx;

public class Battler
{
    public string Name { get; set; }
    public readonly ModifiableProperty<int, ICtx> Hp;
    public readonly ModifiableProperty<int, ICtx> Atk;

    public Battler(string name)
    {
        Name = name;
        Hp = new ModifiableProperty<int, ICtx>(100, min: 0, max: 100);
        Atk = new ModifiableProperty<int, ICtx>(10, min: 1);

        Hp.ProcessedDeltas.Subscribe(delta =>
        {
            if (delta.Delta < 0 && delta.Context is AttackCtx ctx)
            {
                Debug.Log($"{ctx.Source} dealt {-delta.Delta} damage to {this}!");
            }
            else if (delta.Delta > 0 && delta.Context is HealCtx ctx)
            {
                Debug.Log($"{ctx.Source} healed {this} for {delta.Delta} HP!");
            }
        });
    }

    public override string ToString() => Name;
}

var hero = new Battler("Hero");
var ally = new Battler("Ally");
var monster = new Battler("Monster");

// Hero attacks monster
int damage = hero.Atk.CurrentValue;
monster.Hp.AddDelta(-damage, new AttackCtx(hero));

// Ally heals hero
ally.Hp.AddDelta(+20, new HealCtx(ally));
```

---

# Roadmap

-   [ ] Unity Samples~/ package
-   [ ] Visual debugging inspector

---

# Repository Layout

```
.
├── src/BrunoCPF.Modifiable/          # SDK library (Godot / .NET / NuGet) — compiles the
│                                     #   source under unity/Runtime; no duplicated code
├── tests/BrunoCPF.Modifiable.Tests/  # engine-free NUnit suite (dotnet test)
├── unity/                            # UPM package (consumed via ?path=unity)
│   ├── package.json
│   ├── Runtime/                      # the actual source — single source of truth
│   │   ├── Core/                     #   pure, closure-free modifiers, filters and evaluators
│   │   └── Common/                   #   reactive ModifiableProperty, value math, bounds
│   └── Samples~/                     # Unity-only MonoBehaviour sample
└── BrunoCPF.Modifiable.slnx
```

The library code lives **once**, under `unity/Runtime`. Unity compiles it via the `.asmdef`;
the `src/` project `<Compile Include>`s the same files for everyone else. Keep that code free
of both `UnityEngine` and `Godot` types — engine-specific glue belongs in `unity/` overlays or
a future Godot addon, never in the shared source.

Unity compiles the shared source with C# 9, so the SDK project pins `<LangVersion>9.0</LangVersion>`:
newer syntax (record structs, file-scoped namespaces, `required`, collection expressions) fails
the .NET build instead of breaking Unity later. New files under `unity/Runtime` need a `.meta` file
with a fresh GUID, because Unity ignores files without one in immutable (git URL) packages.

# Building & Testing

No engine required — the tests validate the exact code both Unity and Godot consume.

```bash
dotnet build BrunoCPF.Modifiable.slnx -c Release   # library + tests
dotnet test  BrunoCPF.Modifiable.slnx -c Release   # engine-free NUnit suite
dotnet format whitespace BrunoCPF.Modifiable.slnx  # fix whitespace; CI runs it with --verify-no-changes
```

Text files use LF endings and UTF-8 without a BOM (`.editorconfig`); `.gitattributes` normalizes
line endings on commit.

---

# License

MIT License © Bruno Fernandes
Free for commercial and non-commercial use.

---

# Contributing

PRs welcome!
