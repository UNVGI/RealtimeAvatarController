using System;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Samples.UI
{
    public sealed class StubMoCapSourceFactory : IMoCapSourceFactory
    {
        public IMoCapSource Create(MoCapSourceConfigBase config)
        {
            var stubConfig = config as StubMoCapSourceConfig;
            if (stubConfig == null)
            {
                throw new ArgumentException(
                    $"StubMoCapSourceConfig is required, but {config?.GetType().Name ?? "null"} was provided.",
                    nameof(config));
            }

            return new StubMoCapSource();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntime()
        {
            try
            {
                RegistryLocator.MoCapSourceRegistry.Register(
                    StubMoCapSource.StubSourceTypeId,
                    new StubMoCapSourceFactory());
            }
            catch (RegistryConflictException ex)
            {
                RegistryLocator.ErrorChannel.Publish(
                    new SlotError(string.Empty, SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
            }
        }
    }
}
