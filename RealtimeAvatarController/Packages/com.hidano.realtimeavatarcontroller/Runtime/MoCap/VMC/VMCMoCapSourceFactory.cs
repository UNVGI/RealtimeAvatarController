using System;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC
{
    /// <summary>
    /// <see cref="VmcMoCapSource"/> インスタンスを生成する Factory
    /// (design.md §5.3 / §10.1, requirements.md 要件 9-3, 9-4, 9-5, 9-7, 9-9, 9-10)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>属性ベース自己登録 (タスク 8-2)</b>:
    /// <see cref="RegisterRuntime"/> が <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c> で
    /// Player ビルドおよびランタイム起動時に <see cref="RegistryLocator.MoCapSourceRegistry"/> へ
    /// typeId="VMC" を登録する (design.md §7・§10.1)。
    /// </para>
    /// <para>
    /// <b>Domain Reload OFF 対応 (要件 9-8)</b>:
    /// <see cref="RegistryLocator.ResetForTest"/> が <c>SubsystemRegistration</c> タイミングで
    /// 先行実行されるため、通常は二重登録による <see cref="RegistryConflictException"/> は発生しない。
    /// 万一発生した場合は <see cref="RegistryLocator.ErrorChannel"/> へ
    /// <see cref="SlotErrorCategory.RegistryConflict"/> として通知し、握り潰さない (要件 9-10)。
    /// </para>
    /// <para>
    /// <b>Editor 自己登録</b>:
    /// Editor 起動時の登録は本ファイルではなく、別ファイル
    /// <c>Editor/MoCap/VMC/VmcMoCapSourceFactoryEditorRegistrar.cs</c> に分離する (タスク 8-3)。
    /// </para>
    /// </remarks>
    public sealed class VMCMoCapSourceFactory : IMoCapSourceFactory
    {
        /// <summary>
        /// VMC ソースの typeId 識別子 (design.md §10.1, requirements.md 要件 9-5)。
        /// </summary>
        public const string VmcSourceTypeId = "VMC";

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// <paramref name="config"/> を <see cref="VMCMoCapSourceConfig"/> にキャストし、
        /// <see cref="VmcMoCapSource"/> を生成して返す (design.md §10.1)。
        /// </para>
        /// <para>
        /// 生成時の <c>slotId</c> は <see cref="string.Empty"/> を渡し、
        /// <c>MoCapSourceRegistry</c> が後から設定する設計 (design.md §10.1)。
        /// <c>errorChannel</c> は <see cref="RegistryLocator.ErrorChannel"/> を使用する。
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="config"/> が <see cref="VMCMoCapSourceConfig"/> 派生でない場合
        /// (メッセージに実型名を含める / 要件 9-3, 9-4)。
        /// </exception>
        public IMoCapSource Create(MoCapSourceConfigBase config)
        {
            var vmcConfig = config as VMCMoCapSourceConfig;
            if (vmcConfig == null)
            {
                throw new ArgumentException(
                    $"VMCMoCapSourceConfig が必要ですが {config?.GetType().Name ?? "null"} が渡されました。",
                    nameof(config));
            }

            return new VmcMoCapSource(
                slotId: string.Empty,
                errorChannel: RegistryLocator.ErrorChannel);
        }

        /// <summary>
        /// Player ビルドおよびランタイム起動時 (シーンロード前) に
        /// <see cref="RegistryLocator.MoCapSourceRegistry"/> へ typeId="VMC" で自己登録する
        /// (design.md §7・§10.1, タスク 8-2)。
        /// </summary>
        /// <remarks>
        /// Domain Reload OFF 設定下では <c>SubsystemRegistration</c> タイミングで
        /// <see cref="RegistryLocator.ResetForTest"/> が先行実行されるため、
        /// 通常は <see cref="RegistryConflictException"/> は発生しない。
        /// 万一発生した場合は <see cref="RegistryLocator.ErrorChannel"/> へ
        /// <see cref="SlotErrorCategory.RegistryConflict"/> として通知する (要件 9-10)。
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntime()
        {
            try
            {
                RegistryLocator.MoCapSourceRegistry.Register(
                    VmcSourceTypeId,
                    new VMCMoCapSourceFactory());
            }
            catch (RegistryConflictException ex)
            {
                RegistryLocator.ErrorChannel.Publish(
                    new SlotError(string.Empty, SlotErrorCategory.RegistryConflict, ex, DateTime.UtcNow));
            }
        }
    }
}
