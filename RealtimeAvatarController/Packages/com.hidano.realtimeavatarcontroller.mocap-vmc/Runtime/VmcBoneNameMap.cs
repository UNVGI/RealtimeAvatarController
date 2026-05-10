using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC
{
    internal static class VmcBoneNameMap
    {
        private static readonly Dictionary<string, HumanBodyBones> s_map;

        static VmcBoneNameMap()
        {
            s_map = new Dictionary<string, HumanBodyBones>(StringComparer.Ordinal);

            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone)
                {
                    continue;
                }

                s_map.Add(bone.ToString(), bone);
            }
        }

        public static bool TryGetValue(string boneName, out HumanBodyBones bone)
        {
            if (boneName == null)
            {
                bone = default;
                return false;
            }

            return s_map.TryGetValue(boneName, out bone);
        }

        internal static IEnumerable<KeyValuePair<string, HumanBodyBones>> EnumerateForTest()
        {
            return s_map;
        }
    }
}
