#if UNITY_EDITOR
using System;
using RealtimeAvatarController.Core;
using UnityEditor;

namespace RealtimeAvatarController.MoCap.VMC.Editor
{
    /// <summary>
    /// Editor 起動時に <see cref="VMCMoCapSourceFactory"/> を
    /// <see cref="RegistryLocator.MoCapSourceRegistry"/> へ登録する自己登録エントリ
    /// (tasks.md タスク 8-3 / design.md §10.2 / requirements.md 要件 9-5, 9-8, 9-9)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inspector での候補列挙や EditMode テスト・Preview 実行時に
    /// typeId="VMC" が解決できる状態を担保する (要件 9-8)。
    /// </para>
    /// <para>
    /// Runtime 登録は <see cref="VMCMoCapSourceFactory.RegisterRuntime"/> 側で
    /// <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c> により行う (タスク 8-2)。
    /// Domain Reload OFF 設定下でも <see cref="RegistryLocator.ResetForTest"/> が
    /// <c>SubsystemRegistration</c> タイミングで先行実行されるため、
    /// 通常は二重登録による <see cref="RegistryConflictException"/> は発生しない。
    /// 万一発生した場合は <see cref="RegistryLocator.ErrorChannel"/> へ
    /// <see cref="SlotErrorCategory.RegistryConflict"/> として通知し、握り潰さない (要件 9-9)。
    /// </para>
    /// </remarks>
    internal static class VmcMoCapSourceFactoryEditorRegistrar
    {
        /// <summary>
        /// Editor 起動時 (Assembly Reload 完了後) に typeId="VMC" を登録する
        /// (design.md §10.2 / <c>[UnityEditor.InitializeOnLoadMethod]</c>)。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RegisterEditor()
        {
            try
            {
                RegistryLocator.MoCapSourceRegistry.Register(
                    VMCMoCapSourceFactory.VmcSourceTypeId,
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
#endif
