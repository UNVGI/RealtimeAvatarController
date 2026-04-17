using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Builtin
{
    /// <summary>
    /// IAvatarProvider のビルトイン具象実装。
    /// Object.Instantiate / Object.Destroy による Prefab ベースのアバター供給を担う。
    /// </summary>
    public sealed class BuiltinAvatarProvider : IAvatarProvider
    {
        private readonly BuiltinAvatarProviderConfig _config;
        private readonly ISlotErrorChannel _errorChannel;
        private readonly HashSet<GameObject> _managedAvatars = new HashSet<GameObject>();
        private bool _disposed;

        public string ProviderType => "Builtin";

        public BuiltinAvatarProvider(BuiltinAvatarProviderConfig config, ISlotErrorChannel errorChannel)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _config = config;
            _errorChannel = errorChannel ?? RegistryLocator.ErrorChannel;
        }

        public GameObject RequestAvatar(ProviderConfigBase config)
        {
            ThrowIfDisposed();

            var builtinConfig = (config as BuiltinAvatarProviderConfig) ?? _config;
            if (builtinConfig == null)
            {
                var ex = new InvalidOperationException(
                    $"config は BuiltinAvatarProviderConfig でなければなりません。実際の型: {config?.GetType().Name ?? "null"}");
                _errorChannel?.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow));
                throw ex;
            }

            try
            {
                if (builtinConfig.avatarPrefab == null)
                {
                    throw new InvalidOperationException(
                        "BuiltinAvatarProviderConfig.avatarPrefab が null です。Prefab を設定してください。");
                }

                var instance = UnityEngine.Object.Instantiate(builtinConfig.avatarPrefab);
                _managedAvatars.Add(instance);
                return instance;
            }
            catch (Exception ex)
            {
                _errorChannel?.Publish(new SlotError(null, SlotErrorCategory.InitFailure, ex, DateTime.UtcNow));
                throw;
            }
        }

        public UniTask<GameObject> RequestAvatarAsync(ProviderConfigBase config, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var avatar = RequestAvatar(config);
            return UniTask.FromResult(avatar);
        }

        public void ReleaseAvatar(GameObject avatar)
        {
            if (!_managedAvatars.Contains(avatar))
            {
                Debug.LogError("[BuiltinAvatarProvider] ReleaseAvatar: 未管理の GameObject が渡されました。破棄しません。");
                return;
            }

            _managedAvatars.Remove(avatar);
            UnityEngine.Object.Destroy(avatar);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BuiltinAvatarProvider));
            }
        }
    }
}
