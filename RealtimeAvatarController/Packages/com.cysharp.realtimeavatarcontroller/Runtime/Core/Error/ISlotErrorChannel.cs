using System;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Slot エラーの通知チャネル。
    /// UniRx Subject&lt;SlotError&gt; で実装する。
    /// 購読側は .ObserveOnMainThread() でメインスレッドにて受信すること。
    /// <para>
    /// 引き継ぎ事項 (validation-design.md [N-4] / mocap-vmc Spec 向け):
    /// 実装 (<see cref="DefaultSlotErrorChannel"/>) は内部 Subject を <c>Subject.Synchronize()</c> で
    /// ラップしており、<see cref="Publish"/> はワーカースレッドから直接呼び出しても安全である。
    /// <c>VmcReceive</c> 等のワーカースレッド起源エラーをメインスレッドへ移譲してから発行する必要はない。
    /// </para>
    /// </summary>
    public interface ISlotErrorChannel
    {
        /// <summary>Slot エラーの通知ストリーム。発行は抑制なく毎回行う。</summary>
        IObservable<SlotError> Errors { get; }

        /// <summary>
        /// エラーを発行する。
        /// <para>
        /// スレッド安全性: 実装側で <c>Subject.Synchronize()</c> を適用すること。
        /// 購読ストリーム側でメインスレッドに切り替える責務は購読側 (<c>.ObserveOnMainThread()</c>) が負うため、
        /// 呼び出し元はワーカースレッドから直接本メソッドを呼んでも問題ない。
        /// </para>
        /// </summary>
        void Publish(SlotError error);
    }
}
