namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot のライフサイクル状態。
    /// </summary>
    public enum SlotState
    {
        /// <summary>AddSlotAsync 呼び出し後、リソース未初期化。</summary>
        Created,

        /// <summary>Provider / Source の初期化完了後、動作中。</summary>
        Active,

        /// <summary>
        /// リソースを保持したまま一時停止中 (再アクティブ化可能)。
        /// 将来機能。API 未定義 (Active ⇄ Inactive 遷移 API は設計予約済み)。
        /// </summary>
        Inactive,

        /// <summary>RemoveSlotAsync 呼び出し後、または初期化失敗後に全リソース解放済み。</summary>
        Disposed,
    }
}
