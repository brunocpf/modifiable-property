using System;
using BrunoCPF.Modifiable.Common.Properties;
using R3;
using UnityEngine;

namespace BrunoCPF.Modifiable.Samples
{
    public class ModifiablePropertySample : MonoBehaviour
    {
        private ModifiableProperty<float, object> _atk = null!;
        private ModifiableProperty<int, object> _exp = null!;
        private ModifiableProperty<int, object> _level = null!;
        private ModifiableProperty<int, object> _maxHp = null!;
        private ModifiableProperty<int, object> _hp = null!;

        private readonly CompositeDisposable _disposables = new();

        private void Awake()
        {
            _atk = new ModifiableProperty<float, object>(10f, new ValueBounds<float>(0f, int.MaxValue));
            _exp = new ModifiableProperty<int, object>(0, new ValueBounds<int>(0, int.MaxValue));
            _level = new ModifiableProperty<int, object>(1, new ValueBounds<int>(1, 99));
            _maxHp = new ModifiableProperty<int, object>(500, new ValueBounds<int>(1, int.MaxValue));
            _hp = new ModifiableProperty<int, object>(500, new ValueBounds<int>(0, int.MaxValue));

            _atk.AddTo(_disposables);
            _exp.AddTo(_disposables);
            _level.AddTo(_disposables);
            _maxHp.AddTo(_disposables);
            _hp.AddTo(_disposables);

            // Prevent over-heal
            _hp.PushFilter("hp-overflow-protection", delta =>
            {
                int newHp = _hp.CurrentValue + delta.Delta;
                if (newHp > _maxHp.CurrentValue)
                {
                    return delta with { Delta = _maxHp.CurrentValue - _hp.CurrentValue };
                }
                return delta;
            }).AddTo(_disposables);

            // Level reacts to EXP
            _exp.Subscribe(toValue =>
            {
                if (toValue >= 100 && _level.CurrentValue < 2)
                {
                    _level.SetBaseValue(2);
                }
                else if (toValue >= 300 && _level.CurrentValue < 3)
                {
                    _level.SetBaseValue(3);
                }
            }).AddTo(this);

            // EXP is clamped when level changes
            _level.Base.Subscribe(toValue =>
            {
                if (toValue == 1 && (_exp.CurrentValue < 0 || _exp.CurrentValue >= 100))
                {
                    _exp.SetBaseValue(0);
                }
                else if (toValue == 2 && (_exp.CurrentValue < 100 || _exp.CurrentValue >= 300))
                {
                    _exp.SetBaseValue(100);
                }
                else if (toValue == 3 && _exp.CurrentValue < 300)
                {
                    _exp.SetBaseValue(300);
                }
            }).AddTo(_disposables);
        }

        private void Start()
        {
            // --- ATK flow ---
            _atk.AddDelta(+5f);
            _atk.AddDelta(-20f); // clamped
            _atk.SetBaseValue(15f);

            using (var atkPhase = new CompositeDisposable())
            {
                _atk.PushModifier(new ValueModifier<float>("atk-mod", baseValue => baseValue + 10f))
                    .AddTo(atkPhase);
                _atk.AddDelta(+20f);

                _atk.PushFilter("atk-increase-boost", delta =>
                    delta.Delta > 0
                        ? new ValueDelta<float, object>(delta.Delta * 1.5f, delta.Context)
                        : delta).AddTo(atkPhase);

                _atk.AddDelta(+20f); // boosted to +30
            }

            _atk.AddDelta(+10f); // +10 after removing filter

            // --- EXP/Level flow ---
            _exp.AddDelta(100);   // level up to 2
            _exp.AddDelta(200);   // level up to 3
            _level.SetBaseValue(1); // reset to 1 -> exp reset to 0
            _level.SetBaseValue(3); // set level to 3 -> exp to 300
            using (var fixedLevel = _level.PushModifier(new ValueModifier<int>("fixed-level-2", _ => 2)))
            {
                Debug.Log($"Level fixed to {_level.CurrentValue} while base is {_level.Base.CurrentValue}");
            } // dispose -> back to base value

            // --- HP/MaxHP flow ---
            _hp.AddDelta(-100);
            _hp.AddDelta(+10);
            _hp.AddDelta(+200); // capped
            _hp.AddDelta(-300);

            using (var diseasePhase = new CompositeDisposable())
            {
                int cap = _hp.CurrentValue;
                _maxHp.PushModifier(new ValueModifier<int>("disease_cap", v => Math.Min(v, cap)))
                    .AddTo(diseasePhase);
                _hp.ProcessedDeltas.Subscribe(delta =>
                {
                    if (delta.Delta < 0)
                    {
                        cap = _hp.CurrentValue;
                        _maxHp.AddOrUpdateModifier(new ValueModifier<int>("disease_cap", v => Math.Min(v, cap)));
                    }
                }).AddTo(diseasePhase);

                _hp.AddDelta(+50); // capped to disease limit
                _hp.AddDelta(-100);
            }
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}
