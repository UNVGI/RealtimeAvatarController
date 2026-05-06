using System;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC
{
    /// <summary>
    /// <see cref="EVMC4UMoCapSource"/> インスタンスを生成する Factory
    /// (design.md §4.5, requirements.md 要件 5.4, 5.5, 6.1, 6.2, 6.3, 11.3)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>属性ベース自己登録 (タスク 5.2)</b>:
    /// <see cref="RegisterRuntime"/> が <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c> で
    /// Player ビルドおよびランタイム起動時に <see cref="RegistryLocator.MoCapSourceRegistry"/> へ
    /// typeId="VMC" を登録する (design.md §4.5)。
    /// </para>
    /// <para>
    /// <b>Domain Reload OFF 対応 (要件 6.4)</b>:
    /// <see cref="RegistryLocator.ResetForTest"/> が <c>SubsystemRegistration</c> タイミングで
    /// 先行実行されるため、通常は二重登録による <see cref="RegistryConflictException"/> は発生しない。
    /// 万一発生した場合は <see cref="RegistryLocator.ErrorChannel"/> へ
    /// <see cref="SlotErrorCategory.RegistryConflict"/> として通知し、握り潰さない (要件 6.3)。
    /// </para>
    /// <para>
    /// <b>Editor 自己登録</b>:
    /// Editor 起動時の登録は本ファイルではなく、別ファイル
    /// <c>Editor/MoCap/VMC/VmcMoCapSourceFactoryEditorRegistrar.cs</c> に分離する (要件 6.2)。
    /// </para>
    /// </remarks>
    public sealed class VMCMoCapSourceFactory : IMoCapSourceFactory
    {
        /// <summary>
        /// VMC ソースの typeId 識別子 (design.md §4.5, requirements.md 要件 5.4, 11.3)。
        /// 前版 (自前 Vmc 実装) から継続する文字列リテラルを維持する。
        /// </summary>
        public const string VmcSourceTypeId = "VMC";

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// <paramref name="config"/> を <see cref="VMCMoCapSourceConfig"/> にキャストし、
        /// <see cref="EVMC4UMoCapSource"/> を生成して返す (design.md §4.5)。
        /// </para>
        /// <para>
        /// 生成時の <c>slotId</c> は <see cref="string.Empty"/> を渡し、
        /// <c>MoCapSourceRegistry</c> が後から設定する設計。
        /// <c>errorChannel</c> は <see cref="RegistryLocator.ErrorChannel"/> を使用する。
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="config"/> が <see cref="VMCMoCapSourceConfig"/> 派生でない場合
        /// (メッセージに実型名を含める / 要件 5.4)。
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

            return new EVMC4UMoCapSource(
                slotId: string.Empty,
                errorChannel: RegistryLocator.ErrorChannel);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 既定値で <see cref="VMCMoCapSourceConfig"/> を生成して返す。
        /// </remarks>
        public MoCapSourceConfigBase CreateDefaultConfig()
        {
            return ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
        }

        /// <inheritdoc />
        /// <remarks>
        /// VMC は <see cref="HumanoidMotionApplier"/> + LateUpdate 駆動の標準パイプラインに依存するため、
        /// 単発バインドの <c>CreateApplierBridge</c> ではサポートしない。
        /// VMC を <c>RealtimeAvatarSession.AttachMoCapAsync</c> で扱いたい場合は別途 LateUpdate 駆動の
        /// MonoBehaviour を併設する必要がある (Sample: <c>SlotManagerBehaviour</c>)。
        /// </remarks>
        public IDisposable CreateApplierBridge(IMoCapSource source, GameObject avatar, MoCapSourceConfigBase config)
        {
            throw new NotSupportedException(
                "VMC MoCap は LateUpdate 駆動の Humanoid pipeline を必要とするため CreateApplierBridge では未対応です。" +
                "Sample の SlotManagerBehaviour 等を併用してください。");
        }

        /// <summary>
        /// Player ビルドおよびランタイム起動時 (シーンロード前) に
        /// <see cref="RegistryLocator.MoCapSourceRegistry"/> へ typeId="VMC" で自己登録する
        /// (design.md §4.5, 要件 6.1)。
        /// </summary>
        /// <remarks>
        /// Domain Reload OFF 設定下では <c>SubsystemRegistration</c> タイミングで
        /// <see cref="RegistryLocator.ResetForTest"/> が先行実行されるため、
        /// 通常は <see cref="RegistryConflictException"/> は発生しない。
        /// 万一発生した場合は <see cref="RegistryLocator.ErrorChannel"/> へ
        /// <see cref="SlotErrorCategory.RegistryConflict"/> として通知する (要件 6.3)。
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
