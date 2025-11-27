# Modifiable Properties

Reactive, bounded, and modifiable properties for RPG-style stats (HP, ATK, EXP, etc.).

## Install (UPM via Git)

Follow the instructions to install R3 first: [R3 - Reactive Extensions for Unity](https://github.com/Cysharp/R3?tab=readme-ov-file#unity).

Then, add this line to your project's `Packages/manifest.json` under `dependencies`:

```json
"com.brunocpf.modifiable-property": "https://github.com/brunocpf/modifiable-property.git#v0.1.0"
```

If you are consuming from a different tag/branch, replace `v0.1.0` with the desired ref. Unity will also pick up the package when placed in your project's `Packages/` folder as an embedded package.

You can also install it via Unity's Package Manager UI by selecting "Add package from git URL..." and pasting the URL above.

## Runtime assembly

- Assembly: `BrunoCPF.Modifiable`
- Namespace: `BrunoCPF.Modifiable.*`
- Dependency: `com.cysharp.r3`

## Quickstart

```csharp
using BrunoCPF.Modifiable.Common.Properties;

var atk = new ModifiableProperty<float, object>(
    initialValue: 10f,
    bounds: new ValueBounds<float>(0f, int.MaxValue)
);

// Apply a simple delta
atk.AddDelta(+5f);

// Push a modifier that adds +10 to the effective value
using var modHandle = atk.PushModifier(new ValueModifier<float>("atk-mod", baseValue => baseValue + 10f));

// Add a filter that boosts positive deltas by 50%
using var filterHandle = atk.PushFilter("atk-boost", delta =>
{
    if (delta.Delta > 0)
    {
        var boosted = delta.Delta * 1.5f;
        return delta with { Delta = boosted };
    }
    return delta;
});
```

See `Samples~/BasicUsage` for a full walkthrough.

## Samples

- **Basic Usage**: Demonstrates deltas, filters, modifiers, and clamping against bounds.

## Testing

The package ships with `Tests~` (edit mode). Mark the package as testable in your project's `manifest.json`:

```json
"testables": [
  "com.brunocpf.modifiable-property"
]
```

Then run tests via Unity Test Runner.

## License

MIT — see `LICENSE`.
