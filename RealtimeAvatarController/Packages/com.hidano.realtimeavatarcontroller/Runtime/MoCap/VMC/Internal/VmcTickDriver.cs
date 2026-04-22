using System;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Internal
{
    /// <summary>
    /// <see cref="VmcOscAdapter.Tick"/> を Unity MainThread の LateUpdate タイミングで駆動するドライバ (M-3 改訂 2026-04-22)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// VMC Protocol の仕様上、受信 OSC message stream から frame 境界を検知する手段は定義されていないため、
    /// 受信コールバック側では Bone / Root をキャッシュするだけに留め、
    /// 本 MonoBehaviour の <see cref="LateUpdate"/> で 1 フレーム分の flush を trigger する
    /// (EVMC4U 準拠の分離モデル / mocap-vmc design.md §6.1)。
    /// </para>
    /// <para>
    /// <see cref="VmcOscAdapter.Initialize"/> が <c>VmcOscAdapter.uOscServer</c> GameObject に AddComponent して
    /// 使用する。<see cref="VmcOscAdapter.Shutdown"/> で GameObject ごと Destroy されるため、
    /// 本クラスに個別の Dispose 処理は不要。
    /// </para>
    /// </remarks>
    internal sealed class VmcTickDriver : MonoBehaviour
    {
        private Action _tick;

        /// <summary>
        /// LateUpdate で呼び出す callback を登録する。
        /// </summary>
        /// <param name="tick">MainThread で呼び出される処理。通常 <see cref="VmcOscAdapter.Tick"/>。</param>
        internal void Configure(Action tick)
        {
            _tick = tick;
        }

        private void LateUpdate()
        {
            _tick?.Invoke();
        }
    }
}
