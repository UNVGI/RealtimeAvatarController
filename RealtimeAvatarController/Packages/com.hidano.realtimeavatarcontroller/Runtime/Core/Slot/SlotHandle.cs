namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot への参照ハンドル。Slot の状態と設定を読み取る。
    /// <see cref="SlotManager"/> から返されるイミュータブルなスナップショット。
    /// </summary>
    public sealed class SlotHandle
    {
        /// <summary>Slot を一意に識別する ID。</summary>
        public string SlotId { get; }

        /// <summary>エディタ・UI 向け表示名。</summary>
        public string DisplayName { get; }

        /// <summary>Slot のライフサイクル状態。</summary>
        public SlotState State { get; }

        /// <summary>Slot の設定データ。</summary>
        public SlotSettings Settings { get; }

        public SlotHandle(string slotId, string displayName, SlotState state, SlotSettings settings)
        {
            SlotId = slotId;
            DisplayName = displayName;
            State = state;
            Settings = settings;
        }
    }
}
