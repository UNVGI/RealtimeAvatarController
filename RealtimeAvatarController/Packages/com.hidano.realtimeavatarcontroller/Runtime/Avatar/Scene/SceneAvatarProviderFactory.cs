using System;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Scene
{
    /// <summary>
    /// SceneAvatarProvider の Factory。BeforeSceneLoad / Editor 起動時に typeId="Scene" として自己登録する。
    /// </summary>
    public sealed class SceneAvatarProviderFactory : IAvatarProviderFactory
    {
        public const string SceneProviderTypeId = "Scene";

        public IAvatarProvider Create(ProviderConfigBase config)
        {
            var sceneConfig = config as SceneAvatarProviderConfig;
            if (sceneConfig == null)
            {
                throw new ArgumentException(
                    $"Expected SceneAvatarProviderConfig, got {config?.GetType().Name ?? "null"}",
                    nameof(config));
            }

            return new SceneAvatarProvider(sceneConfig);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntime()
        {
            try
            {
                RegistryLocator.ProviderRegistry.Register(
                    SceneProviderTypeId,
                    new SceneAvatarProviderFactory());
            }
            catch (RegistryConflictException ex)
            {
                RegistryLocator.ErrorChannel.Publish(
                    new SlotError(string.Empty, SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditor()
        {
            try
            {
                RegistryLocator.ProviderRegistry.Register(
                    SceneProviderTypeId,
                    new SceneAvatarProviderFactory());
            }
            catch (RegistryConflictException ex)
            {
                RegistryLocator.ErrorChannel.Publish(
                    new SlotError(string.Empty, SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
            }
        }
#endif
    }
}
