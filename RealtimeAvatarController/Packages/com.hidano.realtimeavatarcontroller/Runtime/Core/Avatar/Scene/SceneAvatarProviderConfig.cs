using System;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Scene
{
    /// <summary>
    /// 既にシーンに存在する GameObject を avatar として使う Provider 用の Config。
    /// シーンオブジェクト参照は ScriptableObject にシリアライズできないため、
    /// ランタイムに <c>ScriptableObject.CreateInstance</c> で生成し、<see cref="sceneAvatar"/> を直接代入して使う。
    /// </summary>
    public sealed class SceneAvatarProviderConfig : ProviderConfigBase
    {
        /// <summary>
        /// 紐付け対象のシーン内 GameObject。
        /// SO アセットへ保存しても参照は復元されないため、ランタイム代入専用。
        /// </summary>
        [NonSerialized]
        public GameObject sceneAvatar;
    }
}
