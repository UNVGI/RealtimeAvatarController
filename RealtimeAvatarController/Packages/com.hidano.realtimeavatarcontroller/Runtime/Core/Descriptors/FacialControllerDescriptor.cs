using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// 表情制御の Descriptor (null 許容)。
    /// IEquatable&lt;T&gt; を実装し、Dictionary/HashSet のキーとして安全に使用できる。
    /// Config の等価判定は参照等価 (ReferenceEquals) を使用する。
    /// </summary>
    [Serializable]
    public sealed class FacialControllerDescriptor : IEquatable<FacialControllerDescriptor>
    {
        /// <summary>Registry に登録された具象型を識別するキー (例: "BlendShape", "ARKit")。</summary>
        public string ControllerTypeId;

        /// <summary>
        /// 具象型ごとのコンフィグ。FacialControllerConfigBase (ScriptableObject 派生) を参照する。
        /// Inspector でドラッグ&amp;ドロップ可能。Factory 側はキャストで具象 Config を取得する。
        /// 等価判定は参照等価 (ReferenceEquals) を使用する。
        /// </summary>
        public FacialControllerConfigBase Config;

        /// <summary>
        /// IEquatable&lt;T&gt; 実装。typeId の文字列等価 + Config の参照等価で判定する。
        /// </summary>
        public bool Equals(FacialControllerDescriptor other)
            => other != null
               && ControllerTypeId == other.ControllerTypeId
               && ReferenceEquals(Config, other.Config);

        public override bool Equals(object obj) => Equals(obj as FacialControllerDescriptor);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (ControllerTypeId != null ? ControllerTypeId.GetHashCode() : 0);
                hash = hash * 31 + (Config != null ? RuntimeHelpers.GetHashCode(Config) : 0);
                return hash;
            }
        }

        public static bool operator ==(FacialControllerDescriptor a, FacialControllerDescriptor b)
            => a is null ? b is null : a.Equals(b);
        public static bool operator !=(FacialControllerDescriptor a, FacialControllerDescriptor b)
            => !(a == b);
    }
}
