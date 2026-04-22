using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Internal
{
    /// <summary>
    /// VMC プロトコルの Bone 名 (Unity <see cref="HumanBodyBones"/> 列挙値名と同一文字列) を
    /// <see cref="HumanBodyBones"/> 値へ変換するマッピングテーブル
    /// (design.md §7.1 / requirements.md 要件 5-1)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 静的コンストラクタで <see cref="HumanBodyBones"/> の全列挙値 (<see cref="HumanBodyBones.LastBone"/> 除く) を
    /// <see cref="StringComparer.Ordinal"/> 比較の <see cref="Dictionary{TKey, TValue}"/> へ登録し、
    /// <see cref="TryGetBone"/> による O(1) 照合を提供する。
    /// </para>
    /// </remarks>
    internal static class VmcBoneMapper
    {
        private static readonly Dictionary<string, HumanBodyBones> s_boneMap;

        static VmcBoneMapper()
        {
            s_boneMap = new Dictionary<string, HumanBodyBones>(StringComparer.Ordinal);
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone)
                {
                    continue;
                }
                s_boneMap[bone.ToString()] = bone;
            }
        }

        /// <summary>
        /// VMC Bone 名を <see cref="HumanBodyBones"/> 値へ変換する。
        /// </summary>
        /// <param name="vmcBoneName">VMC プロトコルで受信した Bone 名。<c>null</c> または空文字を許容する。</param>
        /// <param name="bone">変換結果。失敗時は <c>default</c>。</param>
        /// <returns>変換に成功した場合 <c>true</c>、未知の名前または <c>null</c>/空文字の場合 <c>false</c>。</returns>
        public static bool TryGetBone(string vmcBoneName, out HumanBodyBones bone)
        {
            if (string.IsNullOrEmpty(vmcBoneName))
            {
                bone = default;
                return false;
            }
            return s_boneMap.TryGetValue(vmcBoneName, out bone);
        }
    }
}
