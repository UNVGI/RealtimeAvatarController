#if UNITY_EDITOR
using System;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Samples.UI;

namespace RealtimeAvatarController.Samples.UI.Editor
{
    internal static class StubMoCapSourceFactoryEditorRegistrar
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditor()
        {
            try
            {
                RegistryLocator.MoCapSourceRegistry.Register(
                    StubMoCapSource.StubSourceTypeId,
                    new StubMoCapSourceFactory());
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
