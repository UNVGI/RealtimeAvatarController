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
        /// エラー発生時、アバターを非表示にして破綻表示を防ぐ。
        /// アバターに紐付く全 <see cref="UnityEngine.Renderer"/> の <c>enabled</c> を <c>false</c> に
        /// 設定する。<c>GameObject.SetActive(false)</c> は使用しない (motion-pipeline の確定実装と
        /// 統一するため。validation-design.md §11.2 / 引き継ぎ事項 #3)。GameObject は生存させ続け、
        /// 次フレームで正常 Apply が成功した時点で <c>Renderer.enabled = true</c> に復元する
        /// (自動復元の具体実装は Req 13.5 / motion-pipeline 合意後)。
        /// </summary>
        Hide,
    }
}
