using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin
{
    /// <summary>
    /// Configuration asset for the MOVIN MoCap source.
    /// </summary>
    [CreateAssetMenu(
        menuName = "RealtimeAvatarController/MoCap/MOVIN Config",
        fileName = "MovinMoCapSourceConfig")]
    public class MovinMoCapSourceConfig : MoCapSourceConfigBase
    {
        [Range(1, 65535)]
        public int port = 11235;

        [Tooltip("Informational only for uOSC 1.0.0; actual binding uses all interfaces (0.0.0.0).")]
        public string bindAddress = "";

        public string rootBoneName = "";

        public string boneClass = "";
    }
}
