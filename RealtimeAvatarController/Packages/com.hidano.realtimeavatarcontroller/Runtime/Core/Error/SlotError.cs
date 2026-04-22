using System;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot エラー情報。不変オブジェクト。
    /// コンストラクタ引数で全プロパティを初期化し、以降は変更不可。
    /// </summary>
    public sealed class SlotError
    {
        /// <summary>エラーが発生した Slot の識別子。</summary>
        public string SlotId { get; }

        /// <summary>エラーのカテゴリ。</summary>
        public SlotErrorCategory Category { get; }

        /// <summary>エラーの原因となった例外 (存在しない場合は null)。</summary>
        public Exception Exception { get; }

        /// <summary>エラー発生タイムスタンプ (UTC)。</summary>
        public DateTime Timestamp { get; }

        public SlotError(string slotId, SlotErrorCategory category, Exception exception, DateTime timestamp)
        {
            SlotId = slotId;
            Category = category;
            Exception = exception;
            Timestamp = timestamp;
        }
    }
}
