using System;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Builtin
{
    /// <summary>
    /// IAvatarProviderFactory のビルトイン具象実装。
    /// BuiltinAvatarProviderConfig を受け取り BuiltinAvatarProvider を生成する。
    /// ステートレス設計: _errorChannel は読み取り専用参照であり、複数回の Create() 呼び出しが互いに干渉しない。
    /// </summary>
    public sealed class BuiltinAvatarProviderFactory : IAvatarProviderFactory
    {
        /// <summary>ビルトイン Provider の typeId ("Builtin")。</summary>
        public const string BuiltinProviderTypeId = "Builtin";

        private readonly ISlotErrorChannel _errorChannel;

        public BuiltinAvatarProviderFactory(ISlotErrorChannel errorChannel = null)
        {
            _errorChannel = errorChannel;
        }

        public IAvatarProvider Create(ProviderConfigBase config)
        {
            var channel = _errorChannel ?? RegistryLocator.ErrorChannel;

            var builtinConfig = config as BuiltinAvatarProviderConfig;
            if (builtinConfig == null)
            {
                var ex = new ArgumentException(
                    $"Expected BuiltinAvatarProviderConfig, got {config?.GetType().Name ?? "null"}",
                    nameof(config));
                channel?.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow));
                throw ex;
            }

            return new BuiltinAvatarProvider(builtinConfig, channel);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntime()
        {
            try
            {
                RegistryLocator.ProviderRegistry.Register(
                    BuiltinProviderTypeId,
                    new BuiltinAvatarProviderFactory());
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
                    BuiltinProviderTypeId,
                    new BuiltinAvatarProviderFactory());
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
</content>
</invoke>