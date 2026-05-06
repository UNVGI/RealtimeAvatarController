using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin
{
    /// <summary>
    /// Local transform payload for a MOVIN bone pose.
    /// </summary>
    public readonly struct MovinBonePose
    {
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly Vector3? LocalScale;

        public MovinBonePose(
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3? localScale = null)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }
    }

    /// <summary>
    /// Root pose payload for MOVIN /VMC/Ext/Root/Pos messages.
    /// </summary>
    public readonly struct MovinRootPose
    {
        public readonly string BoneName;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly Vector3? LocalScale;
        public readonly Vector3? LocalOffset;

        public MovinRootPose(
            string boneName,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3? localScale = null,
            Vector3? localOffset = null)
        {
            BoneName = boneName;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
            LocalOffset = localOffset;
        }
    }
}
