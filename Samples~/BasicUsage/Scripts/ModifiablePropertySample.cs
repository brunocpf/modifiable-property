using System;
using BrunoCPF.Modifiable.Common.Properties;
using UnityEngine;

namespace BrunoCPF.Modifiable.Samples
{
    // Simple MonoBehaviour demonstrating how to wire deltas, filters, and modifiers.
    public class ModifiablePropertySample : MonoBehaviour
    {
        private ModifiableProperty<int, object> _hp = null!;
        private IDisposable? _hpSubscription;
        private IDisposable? _overflowFilter;
        private IDisposable? _buffModifier;

        private void Awake()
        {
            _hp = new ModifiableProperty<int, object>(
                initialValue: 100,
                bounds: new ValueBounds<int>(0, 200)
            );

            _hpSubscription = _hp.Subscribe(v => Debug.Log($"[HP] {v}"));

            // Prevent over-heal
            _overflowFilter = _hp.PushFilter("hp-overflow-protection", delta =>
            {
                int newHp = _hp.CurrentValue + delta.Delta;
                if (newHp > 200)
                {
                    return delta with { Delta = 200 - _hp.CurrentValue };
                }
                return delta;
            });

            // Flat +10 buff
            _buffModifier = _hp.PushModifier(new ValueModifier<int>("flat-buff", baseValue => baseValue + 10));
        }

        private void Start()
        {
            _hp.AddDelta(-25);  // damage
            _hp.AddDelta(+50);  // heal
            _hp.AddDelta(+200); // capped by overflow filter

            // Remove buff
            _buffModifier?.Dispose();
            _hp.AddDelta(-30);
        }

        private void OnDestroy()
        {
            _hpSubscription?.Dispose();
            _overflowFilter?.Dispose();
            _buffModifier?.Dispose();
            _hp.Dispose();
        }
    }
}
