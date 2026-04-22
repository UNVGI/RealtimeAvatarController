using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Builtin
{
    /// <summary>
    /// ビルトイン Avatar Provider 用の Config。
    /// Inspector からのドラッグ&amp;ドロップ (シナリオ X) と
    /// ScriptableObject.CreateInstance によるランタイム動的生成 (シナリオ Y) の両方をサポートする。
    /// </summary>
    [CreateAssetMenu(
        menuName = "RealtimeAvatarController/BuiltinAvatarProviderConfig",
        fileName = "BuiltinAvatarProviderConfig")]
    public sealed class BuiltinAvatarProviderConfig : ProviderConfigBase
    {
        /// <summary>
        /// アバターとしてインスタンス化する Prefab。
        /// Inspector からのドラッグ&amp;ドロップ、またはランタイムコードからの直接代入に対応する。
        /// </summary>
        public GameObject avatarPrefab;
    }
}
