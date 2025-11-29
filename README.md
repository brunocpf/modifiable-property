# `com.brunocpf.modifiable-property`

_A reactive, extensible stat & value-transformation pipeline for C# and Unity (powered by R3)_

<p align="center">
  <img src="https://img.shields.io/badge/Unity-2021%2B-black?logo=unity" />
  <img src="https://img.shields.io/badge/C%23-9.0%2B-purple?logo=csharp" />
  <img src="https://img.shields.io/badge/R3-Compatible-blue" />
  <img src="https://img.shields.io/badge/License-MIT-green" />
  <br>
  <img src="https://img.shields.io/github/stars/brunocpf/modifiable-property?style=social" />
</p>

---

## What Is This?

**ModifiableProperty** is a powerful, reactive wrapper around a value that supports:

- **Deltas** — incremental changes (damage, healing, EXP gain, gold gain)
- **Filters** — change-time transformations (EXP boosts, healing block, damage reduction)
- **Bounds** — min/max constraints (HP ≥ 0, HP ≤ MaxHP)
- **Modifiers** — view-time transformations (equipment, buffs, multipliers)
- **Disposable Push/Pop Effects** — perfect for RPG buffs, items, equipment, temporary states
- **Context-aware changes** — pass structured metadata with each delta
- **Reactive observation** — subscribe to base, effective, or delta streams
- **Custom numeric support** — define your own math for custom structs

It is ideal for:

- RPG stats
- Character attributes
- Currency systems
- Simulation values
- Buff & debuff systems
- Combat logic
- Ability and item effects

---

## Installation

Add to **Unity Package Manager** via Git URL:

```
https://github.com/brunocpf/modifiable-property.git
```

Or in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.brunocpf.modifiable-property": "https://github.com/brunocpf/modifiable-property.git"
  }
}
```

Requires:

- **R3** → https://github.com/Cysharp/R3

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
var sword = hp.PushModifier(
    id: "sword",
    modifyFunc: v => v + 10
);

Debug.Log(hp.CurrentValue); // → 100

sword.Dispose(); // remove equipment
```

---

# API Overview

```csharp
public sealed class ModifiableProperty<TValue, TContext>
```

### ✔ Add deltas

`AddDelta(delta)`
`AddDelta(ValueDelta<TValue, TContext>)`

### ✔ Add / remove temporary effects

`PushModifier()`
`PushFilter()`

### ✔ Set base value

`SetBaseValue(newValue)`

### ✔ Observe values

- `property.Subscribe(...)`
- `property.Base.Subscribe(...)`
- `property.ProcessedDeltas.Subscribe(...)`

---

# Filters (Change-Time Logic)

Filters modify _deltas_ before they affect the base value.

### EXP Boost

```csharp
exp.PushFilter("boost", d => d with { Delta = (int)(d.Delta * 1.5f) });
```

### Block Healing

```csharp
hp.PushFilter(
    "no-healing",
    d => d.Delta > 0 ? d with { Delta = 0 } : d
);
```

### Minimum Damage Rule (“1 damage minimum”)

```csharp
hp.PushFilter(
    "min-dmg",
    d => d.Delta < 0 && d.Delta > -1 ? new(-1, d.Context) : d
);
```

Filters run in **priority order** (the third argument), lowest first.

---

# Modifiers (View-Time Logic)

Modifiers affect the **final value** after the base is computed.

### Equipment

```csharp
atk.PushModifier("sword", v => v + 20);
```

### Buff

```csharp
atk.PushModifier("atk-up", v => (int)(v * 1.2f));
```

### Debuff

```csharp
atk.PushModifier("atk-down", v => (int)(v * 0.5f));
```

Modifiers also stack in **priority order** (third argument), lowest first.

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
hp.PushFilter("shield", d =>
{
    if (d.Context is DamageContext dc && dc.Source.HasStatus("armor_break"))
    {
        return d; // no reduction
    }

    return d.Delta < 0 ? d with { Delta = d.Delta + 10 } : d;
});
```

---

# Custom Math (Non-Numeric Types)

By default, `ModifiableProperty` supports all built-in numeric types (int, float, double, etc).

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

This is particularly usefult for enums with custom logic (e.g., elemental affinities).

If you don't provide custom math and use a non-numeric type, you might get unexpected behavior.

---

# Integration Patterns

## Buffs

```csharp
IDisposable buff = atk.PushModifier("buff", v => v + 10);
buff.Dispose(); // removes effect
```

## Equipment

```csharp
IDisposable equip = atk.PushModifier("sword", v => v + 25);
equip.Dispose(); // unequip
```

## Blocking Healing

```csharp
var block = hp.PushFilter("block-healing", d =>
    d.Delta > 0 ? d with { Delta = 0 } : d
);

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

### ✔ Give every effect a unique ID

Prevents duplicates and enables precise removal. You can use GUIDs (`System.Guid.NewGuid().ToString()`) for temporary effects.

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

- [ ] Unity Samples~/ package
- [ ] Visual debugging inspector

---

# 📄 License

MIT License © Bruno Fernandes
Free for commercial and non-commercial use.

---

# 🙌 Contributing

PRs welcome!
