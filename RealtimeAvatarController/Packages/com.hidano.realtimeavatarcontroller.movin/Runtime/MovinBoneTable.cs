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
        private static readonly string[] CommonArmatureRootNames =
        {
            "Hips",
            "Pelvis",
            "Armature",
            "Root",
            "RootBone",
            "Skeleton",
        };

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

            var armatureRoot = ResolveArmatureRoot(avatarRoot, rootBoneName);

            var result = new Dictionary<string, Transform>();
            var requiredPrefix = string.IsNullOrWhiteSpace(boneClass) ? null : $"{boneClass}:";
            Construct(armatureRoot, requiredPrefix, result);

            table = result;
            return true;
        }

        /// <summary>
        /// Armature の探索起点を決定する。優先順:
        /// 1) 明示指定の <paramref name="rootBoneName"/>
        /// 2) Humanoid Avatar を持つ Animator が乗っている GameObject
        /// 3) Renderer 兄弟ヒューリスティック（FBX 由来の Body/Armature レイアウト想定）
        /// 4) 一般的な armature root 名（"Hips" 等）への名前一致
        /// 5) フォールバックとして avatar 自身（適用処理は失敗させない）
        /// </summary>
        private static Transform ResolveArmatureRoot(Transform avatarRoot, string rootBoneName)
        {
            if (!string.IsNullOrWhiteSpace(rootBoneName))
            {
                var explicitMatch = SearchByName(avatarRoot, rootBoneName);
                if (explicitMatch != null)
                {
                    return explicitMatch;
                }

                Debug.LogWarning(
                    $"[MovinBoneTable] rootBoneName='{rootBoneName}' に一致する Transform が見つかりませんでした。自動推定にフォールバックします。");
            }

            var humanoid = TryFindHumanoidAnimatorRoot(avatarRoot);
            if (humanoid != null)
            {
                return humanoid;
            }

            var bySibling = SearchBySiblingHeuristic(avatarRoot);
            if (bySibling != null)
            {
                return bySibling;
            }

            foreach (var name in CommonArmatureRootNames)
            {
                var byCommonName = SearchByName(avatarRoot, name);
                if (byCommonName != null)
                {
                    return byCommonName;
                }
            }

            Debug.LogWarning(
                $"[MovinBoneTable] Armature 推定に失敗したため avatar root '{avatarRoot.name}' をそのまま起点として使用します。" +
                "MOVIN bone 名と一致する Transform 名を全階層から探索します。");
            return avatarRoot;
        }

        private static Transform TryFindHumanoidAnimatorRoot(Transform avatarRoot)
        {
            var animators = avatarRoot.GetComponentsInChildren<Animator>(includeInactive: true);
            foreach (var animator in animators)
            {
                if (animator == null) continue;
                var avatar = animator.avatar;
                if (avatar != null && avatar.isHuman)
                {
                    return animator.transform;
                }
            }
            return null;
        }

        private static Transform SearchByName(Transform root, string targetName)
        {
            if (string.Equals(root.name, targetName, StringComparison.Ordinal))
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = SearchByName(root.GetChild(i), targetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform SearchBySiblingHeuristic(Transform root)
        {
            if (HasRendererSibling(root) && root.GetComponent<Renderer>() == null)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = SearchBySiblingHeuristic(root.GetChild(i));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool HasRendererSibling(Transform transform)
        {
            var parent = transform.parent;
            if (parent == null) return false;

            for (var i = 0; i < parent.childCount; i++)
            {
                var sibling = parent.GetChild(i);
                if (sibling == transform) continue;
                if (sibling.GetComponent<Renderer>() != null) return true;
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
