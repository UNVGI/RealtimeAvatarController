using System;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin
{
    /// <summary>
    /// Factory for creating MOVIN MoCap source instances and registering the MOVIN source type.
    /// </summary>
    public sealed class MovinMoCapSourceFactory : IMoCapSourceFactory
    {
        public const string MovinSourceTypeId = "MOVIN";

        public IMoCapSource Create(MoCapSourceConfigBase config)
        {
            var movinConfig = config as MovinMoCapSourceConfig;
            if (movinConfig == null)
            {
                var actualTypeName = config?.GetType().Name ?? "null";
                throw new ArgumentException(
                    $"MovinMoCapSourceConfig is required, but {actualTypeName} was provided.",
                    nameof(config));
            }

            return new MovinMoCapSource();
        }

        public MoCapSourceConfigBase CreateDefaultConfig()
        {
            return ScriptableObject.CreateInstance<MovinMoCapSourceConfig>();
        }

        public IDisposable CreateApplierBridge(IMoCapSource source, GameObject avatar, MoCapSourceConfigBase config)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (avatar == null) throw new ArgumentNullException(nameof(avatar));

            var movinConfig = config as MovinMoCapSourceConfig;
            if (movinConfig == null)
            {
                throw new ArgumentException(
                    $"MovinMoCapSourceConfig is required, but {config?.GetType().Name ?? "null"} was provided.",
                    nameof(config));
            }

            MovinMotionApplier applier = null;
            MovinSlotBridge bridge = null;
            try
            {
                applier = new MovinMotionApplier();
                applier.SetAvatar(avatar, movinConfig.rootBoneName, movinConfig.boneClass);
                bridge = new MovinSlotBridge(source, applier);
                return new MovinApplierAttachment(bridge, applier);
            }
            catch
            {
                bridge?.Dispose();
                applier?.Dispose();
                throw;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntime()
        {
            try
            {
                RegistryLocator.MoCapSourceRegistry.Register(
                    MovinSourceTypeId,
                    new MovinMoCapSourceFactory());
            }
            catch (RegistryConflictException ex)
            {
                RegistryLocator.ErrorChannel.Publish(
                    new SlotError(
                        MovinSourceTypeId,
                        SlotErrorCategory.RegistryConflict,
                        ex,
                        DateTime.UtcNow));
            }
        }

        private sealed class MovinApplierAttachment : IDisposable
        {
            private MovinSlotBridge _bridge;
            private MovinMotionApplier _applier;

            public MovinApplierAttachment(MovinSlotBridge bridge, MovinMotionApplier applier)
            {
                _bridge = bridge;
                _applier = applier;
            }

            public void Dispose()
            {
                _bridge?.Dispose();
                _bridge = null;
                _applier?.Dispose();
                _applier = null;
            }
        }
    }
}
