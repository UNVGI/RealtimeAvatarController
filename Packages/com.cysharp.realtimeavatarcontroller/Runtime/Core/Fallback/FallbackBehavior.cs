namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Applier (モーション適用処理) でエラーが発生した際の Slot フォールバック挙動。
    /// SlotSettings.fallbackBehavior フィールドで Slot ごとに設定する。
    /// </summary>
    public enum FallbackBehavior
    {
        /// <summary>エラー発生時、直前フレームのポーズを維持し続ける (デフォルト)。</summary>
        HoldLastPose,

        /// <summary>エラー発生時、アバターを T ポーズに戻す。デバッグ用途に適する。</summary>
        TPose,

        /// <summary>
        /// エラー発生時、アバターを非表示にする。破綻表示を防ぐ。
        /// <c>Renderer.enabled = false</c> を使用する。<c>GameObject.SetActive(false)</c> は使用しない。
        /// </summary>
        Hide,
    }
}
