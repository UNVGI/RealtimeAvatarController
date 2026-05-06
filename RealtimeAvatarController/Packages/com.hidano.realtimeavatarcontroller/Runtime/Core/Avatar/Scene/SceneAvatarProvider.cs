using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Scene
{
    /// <summary>
    /// 既にシーンに存在する GameObject をそのまま avatar として返す Provider。
    /// Instantiate / Destroy を行わないため、シーンオブジェクトの所有権は呼び出し側に残る。
    /// 利用シナリオ: VTuber 統合側がキャラを自前で配置済みで、その GameObject を MoCap に紐づけたい場合。
    /// </summary>
    public sealed class SceneAvatarProvider : IAvatarProvider
    {
        public string ProviderType => SceneAvatarProviderFactory.SceneProviderTypeId;

        private readonly GameObject _sceneAvatar;
        private bool _disposed;

        public SceneAvatarProvider(SceneAvatarProviderConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.sceneAvatar == null)
            {
                throw new InvalidOperationException(
                    "SceneAvatarProviderConfig.sceneAvatar が null です。シーン内 GameObject をランタイムで割り当ててください。");
            }

            _sceneAvatar = config.sceneAvatar;
        }

        public GameObject RequestAvatar(ProviderConfigBase config)
        {
            ThrowIfDisposed();
            return _sceneAvatar;
        }

        public UniTask<GameObject> RequestAvatarAsync(ProviderConfigBase config, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(RequestAvatar(config));
        }

        public void ReleaseAvatar(GameObject avatar)
        {
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SceneAvatarProvider));
            }
        }
    }
}
