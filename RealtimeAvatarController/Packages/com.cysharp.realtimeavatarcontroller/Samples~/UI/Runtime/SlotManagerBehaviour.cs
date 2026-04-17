using UnityEngine;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Samples.UI
{
    /// <summary>
    /// デモシーン用 SlotManager ラッパー MonoBehaviour。
    /// Awake で SlotManager を初期化し、OnDestroy で Dispose する。
    /// </summary>
    public class SlotManagerBehaviour : MonoBehaviour
    {
        [SerializeField] private SlotSettings[] initialSlots;

        public SlotManager SlotManager { get; private set; }

        private void Awake()
        {
            SlotManager = new SlotManager(
                RegistryLocator.ProviderRegistry,
                RegistryLocator.MoCapSourceRegistry,
                RegistryLocator.ErrorChannel);

            if (initialSlots != null)
            {
                foreach (var settings in initialSlots)
                {
                    if (settings != null)
                        _ = SlotManager.AddSlotAsync(settings);
                }
            }
        }

        private void OnDestroy() => SlotManager?.Dispose();
    }
}
