using System.Collections.Generic;
using UnityEngine;

namespace MOVIN.Core
{
    public class MocapReceiver : VMCReceiver
    {
        [Header("Bone Search Filter")]
        [SerializeField] string rootBoneName;
        [SerializeField] string boneClass;

        private List<Transform> boneTransforms;
        private Dictionary<string, Transform> name2Transform;

        protected override void OnEnable()
        {
            base.OnEnable();
            Build(rootBoneName, boneClass);
            OnRootPose += OnRootPoseHandler;
            OnBonePose += OnBonePoseHandler;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            OnRootPose -= OnRootPoseHandler;
            OnBonePose -= OnBonePoseHandler;
            boneTransforms = null;
            name2Transform = null;
        }

        private Transform SearchArmature(Transform root, string armatureBoneName = "")
        {
            bool IsArmature(Transform trs)
            {
                if (!string.IsNullOrWhiteSpace(armatureBoneName))
                    return trs.name == armatureBoneName;

                if (trs.GetComponent<Renderer>())
                    return false;

                if (trs.parent == null)
                    return false;

                var parent = trs.parent;

                for (int i = 0; i < parent.childCount; ++i)
                {
                    var child = parent.GetChild(i);
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null)
                        return true;
                }

                return false;
            }

            if (IsArmature(root))
                return root;

            for (int i = 0; i < root.childCount; ++i)
            {
                var each = root.GetChild(i);
                var armature = SearchArmature(each, armatureBoneName);

                if (armature != null)
                    return armature;
            }

            return null;
        }

        private bool Build(string armatureBoneName = "", string boneClass = "")
        {
            boneTransforms = new List<Transform>();
            name2Transform = new Dictionary<string, Transform>();
            var armature = SearchArmature(transform, armatureBoneName);

            if (!armature)
            {
                return false;
            }

            void Construct(Transform trs, string boneClass)
            {
                if (!string.IsNullOrWhiteSpace(boneClass) && !trs.name.StartsWith(boneClass))
                    return;

                boneTransforms.Add(trs);
                name2Transform[trs.name] = trs;

                for (int i = 0; i < trs.childCount; ++i)
                    Construct(trs.GetChild(i), boneClass);
            }

            Construct(armature, !string.IsNullOrWhiteSpace(boneClass) ? $"{boneClass}:" : "");

            return true;
        }

        private void OnBonePoseHandler(string boneName, Vector3 localPos, Quaternion localOrientation)
        {
            if (!name2Transform.ContainsKey(boneName))
                return;

            name2Transform[boneName].SetLocalPositionAndRotation(localPos, localOrientation);
        }

        private void OnRootPoseHandler(string boneName, Vector3 localPos, Quaternion localOrientation, Vector3? localScale, Vector3? localOffset)
        {
            if (!name2Transform.ContainsKey(boneName))
                return;

            name2Transform[boneName].SetLocalPositionAndRotation(localPos, localOrientation);

            if (localScale.HasValue)
                name2Transform[boneName].localScale = localScale.Value;
        }
    }
}