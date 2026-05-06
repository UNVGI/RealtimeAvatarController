using System;
using Cysharp.Threading.Tasks;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin.Samples
{
    /// <summary>
    /// <see cref="RealtimeAvatarSession"/> 経由でシーン内アバターに MOVIN MoCap を紐づける最小サンプル。
    /// SO アセット (SlotSettings / AvatarProviderConfig) を一切作らず、Inspector で
    /// アバター GameObject と任意の port 上書きだけ設定すれば動作する。
    /// </summary>
    public sealed class MovinSessionDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("MOVIN モーションを流すシーン内 GameObject。空なら自分自身を使用。")]
        private GameObject avatar;

        [SerializeField, Tooltip("既定値 (11235) を変えたいときだけ true にする。")]
        private bool overridePort;

        [SerializeField, Range(1, 65535)]
        private int port = 11235;

        [SerializeField, Tooltip("MOVIN bone 名 prefix フィルタ。空なら全 bone 対象。")]
        private string boneClass = "";

        [SerializeField, Tooltip("Bone table 探索起点 Transform 名。空なら sample 由来の armature 自動探索。")]
        private string rootBoneName = "";

        private AttachedSession _session;

        private async void Awake()
        {
            var target = avatar != null ? avatar : gameObject;

            MoCapSourceConfigBase configOverride = null;
            if (overridePort || !string.IsNullOrEmpty(boneClass) || !string.IsNullOrEmpty(rootBoneName))
            {
                var movinConfig = ScriptableObject.CreateInstance<MovinMoCapSourceConfig>();
                movinConfig.port = overridePort ? port : 11235;
                movinConfig.boneClass = boneClass ?? string.Empty;
                movinConfig.rootBoneName = rootBoneName ?? string.Empty;
                configOverride = movinConfig;
            }

            try
            {
                _session = await RealtimeAvatarSession.AttachMoCapAsync(
                    target,
                    MovinMoCapSourceFactory.MovinSourceTypeId,
                    configOverride);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MovinSessionDriver] AttachMoCapAsync failed: {ex}");
            }
        }

        private void OnDestroy()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
