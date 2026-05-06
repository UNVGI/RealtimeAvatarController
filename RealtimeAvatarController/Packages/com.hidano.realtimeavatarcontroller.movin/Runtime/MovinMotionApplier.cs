using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin
{
    /// <summary>
    /// Applies MOVIN frames directly to matching avatar Transforms.
    /// </summary>
    public sealed class MovinMotionApplier : IDisposable
    {
        private Dictionary<string, Transform> _boneTable;

        public void SetAvatar(GameObject avatarRoot, string rootBoneName, string boneClass)
        {
            _boneTable = null;

            var rootTransform = avatarRoot != null ? avatarRoot.transform : null;
            if (!MovinBoneTable.TryBuild(rootTransform, rootBoneName, boneClass, out var table))
            {
                var avatarName = avatarRoot != null ? avatarRoot.name : "<null>";
                throw new InvalidOperationException(
                    $"Failed to build MOVIN bone table for avatar '{avatarName}' " +
                    $"(rootBoneName='{rootBoneName ?? string.Empty}', boneClass='{boneClass ?? string.Empty}').");
            }

            _boneTable = table;
        }

        public void Apply(MovinMotionFrame frame)
        {
            if (frame == null || _boneTable == null)
            {
                return;
            }

            foreach (var pair in frame.Bones)
            {
                var pose = pair.Value;
                ApplyPose(pair.Key, pose.LocalPosition, pose.LocalRotation, pose.LocalScale);
            }

            if (frame.RootPose.HasValue)
            {
                var rootPose = frame.RootPose.Value;
                ApplyPose(rootPose.BoneName, rootPose.LocalPosition, rootPose.LocalRotation, rootPose.LocalScale);
            }
        }

        public void Dispose()
        {
            _boneTable = null;
        }

        private void ApplyPose(
            string boneName,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3? localScale)
        {
            if (string.IsNullOrEmpty(boneName) ||
                _boneTable == null ||
                !_boneTable.TryGetValue(boneName, out var transform) ||
                transform == null)
            {
                return;
            }

            transform.SetLocalPositionAndRotation(localPosition, localRotation);
            if (localScale.HasValue)
            {
                transform.localScale = localScale.Value;
            }
        }
    }
}
