using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RealtimeAvatarController.Core;
using UniRx;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin.Samples
{
    /// <summary>
    /// Sample MonoBehaviour that wires MOVIN Slot resources into the MOVIN apply pipeline.
    /// </summary>
    public sealed class MovinSlotDriver : MonoBehaviour
    {
        [SerializeField] private SlotSettings[] initialSlots;
        [SerializeField] private Transform avatarRoot;

        private readonly Dictionary<string, Pipeline> _pipelines = new Dictionary<string, Pipeline>();
        private CompositeDisposable _disposables;

        public SlotManager SlotManager { get; private set; }

        private void Awake()
        {
            SlotManager = new SlotManager(
                RegistryLocator.ProviderRegistry,
                RegistryLocator.MoCapSourceRegistry,
                RegistryLocator.ErrorChannel);

            _disposables = new CompositeDisposable();
            SlotManager.OnSlotStateChanged
                .ObserveOnMainThread()
                .Subscribe(OnSlotStateChanged)
                .AddTo(_disposables);

            if (initialSlots == null)
            {
                return;
            }

            foreach (var settings in initialSlots)
            {
                if (settings != null)
                {
                    AddInitialSlotAsync(settings).Forget();
                }
            }
        }

        private async UniTaskVoid AddInitialSlotAsync(SlotSettings settings)
        {
            try
            {
                await SlotManager.AddSlotAsync(settings);
            }
            catch (Exception ex)
            {
                PublishInitFailure(settings != null ? settings.slotId : string.Empty, ex);
            }
        }

        private void OnSlotStateChanged(SlotStateChangedEvent e)
        {
            if (e == null)
            {
                return;
            }

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
            if (string.IsNullOrEmpty(slotId) || _pipelines.ContainsKey(slotId))
            {
                return;
            }

            var settings = SlotManager.GetSlot(slotId)?.Settings;
            if (!IsMovinSlot(settings))
            {
                return;
            }

            var config = settings.moCapSourceDescriptor.Config as MovinMoCapSourceConfig;
            if (config == null)
            {
                PublishInitFailure(
                    slotId,
                    new InvalidOperationException("MOVIN Slot requires MovinMoCapSourceConfig."));
                return;
            }

            if (!SlotManager.TryGetSlotResources(slotId, out var source, out var resolvedAvatar))
            {
                PublishInitFailure(
                    slotId,
                    new InvalidOperationException("Slot resources are not available for MOVIN pipeline setup."));
                return;
            }

            var avatarObject = ResolveAvatarObject(resolvedAvatar);
            if (avatarObject == null)
            {
                PublishInitFailure(
                    slotId,
                    new InvalidOperationException("Avatar root is not assigned or resolved."));
                return;
            }

            MovinMotionApplier applier = null;
            MovinSlotBridge bridge = null;
            try
            {
                applier = new MovinMotionApplier();
                applier.SetAvatar(avatarObject, config.rootBoneName, config.boneClass);

                bridge = new MovinSlotBridge(source, applier);
                _pipelines.Add(slotId, new Pipeline(bridge, applier));
            }
            catch (Exception ex)
            {
                bridge?.Dispose();
                applier?.Dispose();
                PublishInitFailure(slotId, ex);
            }
        }

        private GameObject ResolveAvatarObject(GameObject resolvedAvatar)
        {
            if (avatarRoot != null)
            {
                return avatarRoot.gameObject;
            }

            return resolvedAvatar;
        }

        private void TeardownPipeline(string slotId)
        {
            if (!_pipelines.TryGetValue(slotId, out var pipeline))
            {
                return;
            }

            pipeline.Dispose();
            _pipelines.Remove(slotId);
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
            _disposables = null;

            foreach (var pipeline in _pipelines.Values)
            {
                pipeline.Dispose();
            }

            _pipelines.Clear();

            SlotManager?.Dispose();
            SlotManager = null;
        }

        private static bool IsMovinSlot(SlotSettings settings)
        {
            return string.Equals(
                settings?.moCapSourceDescriptor?.SourceTypeId,
                MovinMoCapSourceFactory.MovinSourceTypeId,
                StringComparison.Ordinal);
        }

        private static void PublishInitFailure(string slotId, Exception ex)
        {
            var errorSlotId = string.IsNullOrEmpty(slotId)
                ? MovinMoCapSourceFactory.MovinSourceTypeId
                : slotId;

            RegistryLocator.ErrorChannel.Publish(
                new SlotError(errorSlotId, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow));
        }

        private sealed class Pipeline : IDisposable
        {
            private readonly MovinSlotBridge _bridge;
            private readonly MovinMotionApplier _applier;
            private bool _disposed;

            public Pipeline(MovinSlotBridge bridge, MovinMotionApplier applier)
            {
                _bridge = bridge;
                _applier = applier;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _bridge?.Dispose();
                _applier?.Dispose();
            }
        }
    }
}
