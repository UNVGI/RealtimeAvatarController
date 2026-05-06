using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.Movin
{
    /// <summary>
    /// Builds a received bone-name to avatar Transform lookup table for MOVIN frames.
    /// </summary>
    internal static class MovinBoneTable
    {
        public static bool TryBuild(
            Transform avatarRoot,
            string rootBoneName,
            string boneClass,
            out Dictionary<string, Transform> table)
        {
            table = null;

            if (avatarRoot == null)
            {
                return false;
            }

            var armatureRoot = SearchArmature(avatarRoot, rootBoneName);
            if (armatureRoot == null)
            {
                return false;
            }

            var result = new Dictionary<string, Transform>();
            var requiredPrefix = string.IsNullOrWhiteSpace(boneClass) ? null : $"{boneClass}:";
            Construct(armatureRoot, requiredPrefix, result);

            table = result;
            return true;
        }

        private static Transform SearchArmature(Transform root, string rootBoneName)
        {
            if (IsArmature(root, rootBoneName))
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var armature = SearchArmature(root.GetChild(i), rootBoneName);
                if (armature != null)
                {
                    return armature;
                }
            }

            return null;
        }

        private static bool IsArmature(Transform transform, string rootBoneName)
        {
            if (!string.IsNullOrWhiteSpace(rootBoneName))
            {
                return transform.name == rootBoneName;
            }

            if (transform.GetComponent<Renderer>() != null)
            {
                return false;
            }

            var parent = transform.parent;
            if (parent == null)
            {
                return false;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).GetComponent<Renderer>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Construct(
            Transform transform,
            string requiredPrefix,
            Dictionary<string, Transform> table)
        {
            if (string.IsNullOrWhiteSpace(requiredPrefix) ||
                transform.name.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                table[transform.name] = transform;
            }

            for (var i = 0; i < transform.childCount; i++)
            {
                Construct(transform.GetChild(i), requiredPrefix, table);
            }
        }
    }
}
