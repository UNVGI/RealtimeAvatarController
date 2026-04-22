using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// アバター (GameObject) の供給・解放を担うプロバイダインターフェース。
    /// 同期・非同期のいずれの具象実装も許容する。
    /// ビルトイン Provider は同期版を実装し、Addressable Provider は非同期版を実装する。
    /// </summary>
    public interface IAvatarProvider : IDisposable
    {
        /// <summary>Provider 種別識別子 (例: "Builtin", "Addressable")</summary>
        string ProviderType { get; }

        /// <summary>
        /// アバターを同期的に要求する。
        /// 非同期 Provider では NotSupportedException をスローしてよい。
        /// </summary>
        GameObject RequestAvatar(ProviderConfigBase config);

        /// <summary>
        /// アバターを非同期に要求する。UniTask を採用。
        /// 同期 Provider は同期完了の UniTask を返してよい。
        /// </summary>
        UniTask<GameObject> RequestAvatarAsync(ProviderConfigBase config, CancellationToken cancellationToken = default);

        /// <summary>
        /// 供給したアバターを解放する。
        /// </summary>
        void ReleaseAvatar(GameObject avatar);
    }
}
