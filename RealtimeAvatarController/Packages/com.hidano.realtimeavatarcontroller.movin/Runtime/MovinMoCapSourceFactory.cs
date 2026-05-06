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
    }
}
