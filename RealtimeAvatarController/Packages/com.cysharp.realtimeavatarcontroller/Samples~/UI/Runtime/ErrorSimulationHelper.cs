using System;
using UnityEngine;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// デモシーン用のエラーシミュレーションヘルパー。
    /// VMC 切断・初期化失敗を擬似発生させ <see cref="ISlotErrorChannel"/> 経由で
    /// <see cref="SlotErrorPanel"/> (T14 実装) に通知させるためのテスト専用ユーティリティ。
    /// design.md §7.3 の「エラーシミュレーション」ボタンから呼び出される想定。
    /// </summary>
    public static class ErrorSimulationHelper
    {
        /// <summary>
        /// 指定 Slot の <see cref="IMoCapSource"/> に接続タイムアウトを擬似発生させ、
        /// <see cref="SlotErrorCategory.VmcReceive"/> を <see cref="ISlotErrorChannel"/> に発行する。
        /// 実際の <c>IMoCapSource</c> を操作せず、擬似的な <see cref="TimeoutException"/> を生成して
        /// <see cref="RegistryLocator.ErrorChannel"/> へ直接 <see cref="ISlotErrorChannel.Publish"/> する。
        /// </summary>
        /// <param name="slotId">エラーが発生したことにする Slot ID (null の場合は空文字列として発行)。</param>
        public static void SimulateVmcDisconnect(string slotId)
        {
            var exception = new TimeoutException(
                $"[ErrorSimulation] VMC 接続タイムアウトをシミュレートしました (slotId='{slotId}')。");
            var error = new SlotError(
                slotId ?? string.Empty,
                SlotErrorCategory.VmcReceive,
                exception,
                DateTime.UtcNow);
            RegistryLocator.ErrorChannel.Publish(error);
        }

        /// <summary>
        /// 不正な typeId (<c>"INVALID_TYPE"</c>) を持つ <see cref="SlotSettings"/> を
        /// <see cref="SlotManager.AddSlotAsync"/> に渡して <see cref="SlotErrorCategory.InitFailure"/> を発生させる。
        /// <see cref="SlotManager"/> 側の <c>HandleInitFailure</c> が Provider/Source Resolve 失敗を捕捉し
        /// <see cref="ISlotErrorChannel"/> へ <see cref="SlotErrorCategory.InitFailure"/> を発行する。
        /// <paramref name="slotManager"/> が null の場合は安全のため no-op とする。
        /// </summary>
        /// <param name="slotManager">対象の <see cref="SlotManager"/>。</param>
        public static void SimulateInitFailure(SlotManager slotManager)
        {
            if (slotManager == null)
            {
                Debug.LogWarning("[ErrorSimulationHelper] SlotManager が null のため InitFailure シミュレーションを実行できません。");
                return;
            }

            var settings = ScriptableObject.CreateInstance<SlotSettings>();
            settings.slotId = $"sim-init-failure-{Guid.NewGuid():N}";
            settings.displayName = "InitFailure Simulation";
            settings.avatarProviderDescriptor = new AvatarProviderDescriptor
            {
                ProviderTypeId = "INVALID_TYPE",
            };
            settings.moCapSourceDescriptor = new MoCapSourceDescriptor
            {
                SourceTypeId = "INVALID_TYPE",
            };

            _ = slotManager.AddSlotAsync(settings);
        }
    }
}
