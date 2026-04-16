using System;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Motion
{
    /// <summary>
    /// モーションフレームをアバターに適用するアプライヤーの抽象インターフェース。
    /// Humanoid / Generic など骨格形式ごとに具象クラスを実装する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>スレッド要件</b>: 本インターフェースの全メソッドは Unity メインスレッド
    /// (推奨: <c>LateUpdate</c> タイミング) からのみ呼び出すこと。
    /// ワーカースレッドからの呼び出しは未定義動作となる。
    /// </para>
    /// <para>
    /// <b>エラー通知の責務分担</b>: <see cref="Apply"/> 内の例外は catch せず
    /// 呼び出し元 (SlotManager) に伝搬する。<c>FallbackBehavior</c> の実行および
    /// <c>ISlotErrorChannel</c> への通知は SlotManager の責務であり、
    /// Applier 自体は <c>ISlotErrorChannel</c> への参照を保持しない。
    /// </para>
    /// </remarks>
    public interface IMotionApplier : IDisposable
    {
        /// <summary>
        /// アバターにモーションを適用する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>スレッド要件</b>: Unity メインスレッド (<c>LateUpdate</c> タイミング推奨) からのみ呼び出すこと。
        /// </para>
        /// <para>
        /// <b>スキップ条件 (例外をスローしない)</b>:
        /// <paramref name="frame"/> が <c>null</c>、無効フレーム
        /// (例: <c>HumanoidMotionFrame.IsValid == false</c>)、
        /// または具象 Applier が扱えない骨格形式の場合は適用をスキップする。
        /// </para>
        /// <para>
        /// 適用処理中に例外が発生した場合は catch せず呼び出し元に伝搬する。
        /// </para>
        /// </remarks>
        /// <param name="frame">適用するフレーム。<c>null</c> または無効フレームの場合はスキップ。</param>
        /// <param name="weight">
        /// 適用ウェイト (0.0〜1.0)。
        /// <b>呼び出し元 (SlotManager) が事前に <c>Mathf.Clamp01</c> でクランプした値を渡すこと。</b>
        /// Applier 内部ではクランプ処理を行わない (二重クランプ禁止)。
        /// 初期版の有効値: <c>0.0</c> / <c>1.0</c> の二値。
        /// </param>
        /// <param name="settings">対象 Slot の設定 (<c>fallbackBehavior</c> 等の参照に使用)。</param>
        void Apply(MotionFrame frame, float weight, SlotSettings settings);

        /// <summary>
        /// アバター <see cref="GameObject"/> を設定 / 変更する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>スレッド要件</b>: Unity メインスレッドからのみ呼び出すこと。
        /// </para>
        /// <para>
        /// <c>null</c> を渡した場合はアバターを切り離し、以降の <see cref="Apply"/> 呼び出しをスキップする。
        /// </para>
        /// </remarks>
        /// <param name="avatarRoot">アバターのルート <see cref="GameObject"/>。<c>null</c> で切り離し。</param>
        void SetAvatar(GameObject avatarRoot);
    }
}
