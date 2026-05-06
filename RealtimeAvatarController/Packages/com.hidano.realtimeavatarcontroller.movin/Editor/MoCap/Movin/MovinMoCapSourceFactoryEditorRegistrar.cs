#if UNITY_EDITOR
using System;
using RealtimeAvatarController.Core;
using UnityEditor;

namespace RealtimeAvatarController.MoCap.Movin.Editor
{
    /// <summary>
    /// Registers the MOVIN MoCap source factory when the Unity editor loads assemblies.
    /// </summary>
    internal static class MovinMoCapSourceFactoryEditorRegistrar
    {
        [InitializeOnLoadMethod]
        private static void RegisterEditor()
        {
            try
            {
                RegistryLocator.MoCapSourceRegistry.Register(
                    MovinMoCapSourceFactory.MovinSourceTypeId,
                    new MovinMoCapSourceFactory());
            }
            catch (RegistryConflictException ex)
            {
                RegistryLocator.ErrorChannel.Publish(
                    new SlotError(
                        MovinMoCapSourceFactory.MovinSourceTypeId,
                        SlotErrorCategory.RegistryConflict,
                        ex,
                        DateTime.UtcNow));
            }
        }
    }
}
#endif
