namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot 状態変化イベント。
    /// <see cref="SlotManager.OnSlotStateChanged"/> ストリームで通知されるイミュータブルなイベント。
    /// </summary>
    public sealed class SlotStateChangedEvent
    {
        /// <summary>状態が変化した Slot の ID。</summary>
        public string SlotId { get; }

        /// <summary>遷移前の状態。</summary>
        public SlotState PreviousState { get; }

        /// <summary>遷移後の状態。</summary>
        public SlotState NewState { get; }

        public SlotStateChangedEvent(string slotId, SlotState previousState, SlotState newState)
        {
            SlotId = slotId;
            PreviousState = previousState;
            NewState = newState;
        }
    }
}
</content>
</invoke>