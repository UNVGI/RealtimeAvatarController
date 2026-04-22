using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// デモシーン用 SlotManager ラッパー MonoBehaviour。
    /// Awake で SlotManager を初期化し、Slot ごとに MotionCache / HumanoidMotionApplier を生成・結線する。
    /// LateUpdate で各 Slot の ApplyWithFallback を呼び出してモーション適用パイプラインを駆動する。
    /// OnDestroy で Pipeline と SlotManager を Dispose する。
    /// </summary>
    public class SlotManagerBehaviour : MonoBehaviour
    {
        [SerializeField] private SlotSettings[] initialSlots;

        public SlotManager SlotManager { get; private set; }

        private readonly Dictionary<string, Pipeline> _pipelines = new Dictionary<string, Pipeline>();
        private CompositeDisposable _disposables;

        private void Awake()
        {
            SlotManager = new SlotManager(
                RegistryLocator.ProviderRegistry,
                RegistryLocator.MoCapSourceRegistry,
                RegistryLocator.ErrorChannel);

            _disposables = new CompositeDisposable();
            SlotManager.OnSlotStateChanged
                .Subscribe(OnSlotStateChanged)
                .AddTo(_disposables);

            if (initialSlots != null)
            {
                foreach (var settings in initialSlots)
                {
                    if (settings != null)
                        _ = SlotManager.AddSlotAsync(settings);
                }
            }
        }

        private void OnSlotStateChanged(SlotStateChangedEvent e)
        {
            if (e == null) return;

            if (e.NewState == SlotState.Active)
            {
                BuildPipeline(e.SlotId);
            }
            else if (e.NewState == SlotState.Disposed)
            {
                TeardownPipeline(e.SlotId);
            }
        }

        private void BuildPipeline(string slotId)
        {
            if (_pipelines.ContainsKey(slotId)) return;
            if (!SlotManager.TryGetSlotResources(slotId, out var source, out var avatar)) return;

            var cache = new MotionCache();
            cache.SetSource(source);

            var applier = new HumanoidMotionApplier(slotId);
            try
            {
                applier.SetAvatar(avatar);
            }
            catch (Exception ex)
            {
                // Humanoid でないアバター等。Applier は null のまま扱い、適用しない。
                Debug.LogWarning($"[SlotManagerBehaviour] slotId='{slotId}' Applier.SetAvatar failed: {ex.Message}");
                applier.Dispose();
                cache.Dispose();
                return;
            }

            _pipelines[slotId] = new Pipeline { Cache = cache, Applier = applier };
        }

        private void TeardownPipeline(string slotId)
        {
            if (_pipelines.TryGetValue(slotId, out var p))
            {
                p.Cache?.Dispose();
                p.Applier?.Dispose();
                _pipelines.Remove(slotId);
            }
        }

        private void LateUpdate()
        {
            if (SlotManager == null || _pipelines.Count == 0) return;

            foreach (var kv in _pipelines)
            {
                var slotId = kv.Key;
                var pipeline = kv.Value;
                var handle = SlotManager.GetSlot(slotId);
                if (handle == null) continue;
                var settings = handle.Settings;
                if (settings == null) continue;

                var frame = pipeline.Cache.LatestFrame;
                var weight = settings.weight;
                var capturedApplier = pipeline.Applier;

                SlotManager.ApplyWithFallback(slotId, () => capturedApplier.Apply(frame, weight, settings));
            }
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
            _disposables = null;

            foreach (var kv in _pipelines)
            {
                kv.Value.Cache?.Dispose();
                kv.Value.Applier?.Dispose();
            }
            _pipelines.Clear();

            SlotManager?.Dispose();
            SlotManager = null;
        }

        private struct Pipeline
        {
            public MotionCache Cache;
            public HumanoidMotionApplier Applier;
        }
    }
}
