using System;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot エラーの通知チャネル。
    /// UniRx Subject&lt;SlotError&gt; で実装する。
    /// 購読側は .ObserveOnMainThread() でメインスレッドにて受信すること。
    /// </summary>
    public interface ISlotErrorChannel
    {
        /// <summary>Slot エラーの通知ストリーム。発行は抑制なく毎回行う。</summary>
        IObservable<SlotError> Errors { get; }

        /// <summary>エラーを発行する。</summary>
        void Publish(SlotError error);
    }
}
