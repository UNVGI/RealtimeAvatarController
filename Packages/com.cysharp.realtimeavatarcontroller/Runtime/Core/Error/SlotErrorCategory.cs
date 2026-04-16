namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot エラーのカテゴリ分類。
    /// </summary>
    public enum SlotErrorCategory
    {
        /// <summary>VMC / OSC 受信中のパースエラー・切断検知等。mocap-vmc 担当。</summary>
        VmcReceive,

        /// <summary>Slot 初期化失敗 (Provider/Source の Resolve 失敗、Factory キャスト失敗等)。</summary>
        InitFailure,

        /// <summary>Applier (モーション適用処理) でのエラー。motion-pipeline / slot-core 担当。</summary>
        ApplyFailure,

        /// <summary>Registry への同一 typeId 二重登録。</summary>
        RegistryConflict,
    }
}
