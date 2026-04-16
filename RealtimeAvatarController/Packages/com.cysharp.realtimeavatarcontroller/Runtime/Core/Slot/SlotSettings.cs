using System;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot の設定データモデル。
    /// ScriptableObject を継承しつつ <c>[Serializable]</c> POCO としても機能する。
    /// SO アセット編集 (シナリオ X) と <c>ScriptableObject.CreateInstance</c> によるランタイム動的生成 (シナリオ Y) の
    /// 両方を区別なく許容する。インターフェース型フィールドは保持せず、具象型解決は Registry / Factory が担う。
    /// <para>
    /// <c>weight</c> フィールドは <c>[Range(0f, 1f)]</c> 属性を持つが、実際のクランプ処理は
    /// <c>SlotManager</c> 側で <c>Mathf.Clamp01</c> により実施される。
    /// 初期版では常に 1.0 として使用される (0.0 &lt; weight &lt; 1.0 の中間値セマンティクスは未定義)。
    /// </para>
    /// </summary>
    [Serializable]
    public class SlotSettings : ScriptableObject
    {
        // --- 識別 ---

        /// <summary>Slot を一意に識別する主キー。必須。</summary>
        public string slotId;

        /// <summary>エディタ・UI 向け表示名。必須。</summary>
        public string displayName;

        // --- モーション合成ウェイト ---

        /// <summary>
        /// モーション合成ウェイト (0.0〜1.0)。初期版では常に 1.0 を使用する。
        /// 範囲外の値は <c>SlotManager</c> 側で <c>Mathf.Clamp01</c> によりクランプされる
        /// (本クラス側ではクランプを行わない)。
        /// 将来の複数ソース混合シナリオのためのフックとして保持する。
        /// </summary>
        [Range(0f, 1f)]
        public float weight = 1.0f;

        // --- Descriptor 群 ---

        /// <summary>アバター供給元の Descriptor。必須。</summary>
        public AvatarProviderDescriptor avatarProviderDescriptor;

        /// <summary>MoCap ソースの Descriptor。必須。</summary>
        public MoCapSourceDescriptor moCapSourceDescriptor;

        /// <summary>表情制御の Descriptor。省略可 (null 許容)。</summary>
        public FacialControllerDescriptor facialControllerDescriptor;

        /// <summary>リップシンクソースの Descriptor。省略可 (null 許容)。</summary>
        public LipSyncSourceDescriptor lipSyncSourceDescriptor;

        // --- フォールバック挙動 ---

        /// <summary>
        /// Applier エラー発生時のフォールバック挙動。
        /// デフォルト: <see cref="FallbackBehavior.HoldLastPose"/>。
        /// </summary>
        public FallbackBehavior fallbackBehavior = FallbackBehavior.HoldLastPose;

        // --- バリデーション ---

        /// <summary>
        /// 設定の最低限バリデーション。<c>SlotManager.AddSlotAsync</c> の前に呼ばれる。
        /// 不正な場合は <see cref="InvalidOperationException"/> をスローする。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <c>slotId</c>, <c>displayName</c>, <c>avatarProviderDescriptor.ProviderTypeId</c>,
        /// <c>moCapSourceDescriptor.SourceTypeId</c> のいずれかが未設定のとき。
        /// </exception>
        public void Validate()
        {
            if (string.IsNullOrEmpty(slotId))
                throw new InvalidOperationException("slotId は必須です。");
            if (string.IsNullOrEmpty(displayName))
                throw new InvalidOperationException("displayName は必須です。");
            if (avatarProviderDescriptor == null || string.IsNullOrEmpty(avatarProviderDescriptor.ProviderTypeId))
                throw new InvalidOperationException("avatarProviderDescriptor.ProviderTypeId は必須です。");
            if (moCapSourceDescriptor == null || string.IsNullOrEmpty(moCapSourceDescriptor.SourceTypeId))
                throw new InvalidOperationException("moCapSourceDescriptor.SourceTypeId は必須です。");
        }
    }
}
