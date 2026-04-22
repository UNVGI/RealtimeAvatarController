using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// アバター供給元の Descriptor。
    /// IEquatable&lt;T&gt; を実装し、Dictionary/HashSet のキーとして安全に使用できる。
    /// Config の等価判定は参照等価 (ReferenceEquals) を使用する。
    /// </summary>
    [Serializable]
    public sealed class AvatarProviderDescriptor : IEquatable<AvatarProviderDescriptor>
    {
        /// <summary>Registry に登録された具象型を識別するキー (例: "Builtin", "Addressable")。</summary>
        public string ProviderTypeId;

        /// <summary>
        /// 具象型ごとのコンフィグ。ProviderConfigBase (ScriptableObject 派生) を参照する。
        /// Inspector でドラッグ&amp;ドロップ可能。Factory 側はキャストで具象 Config を取得する。
        /// 等価判定は参照等価 (ReferenceEquals) を使用する。
        /// </summary>
        public ProviderConfigBase Config;

        /// <summary>
        /// IEquatable&lt;T&gt; 実装。typeId の文字列等価 + Config の参照等価で判定する。
        /// </summary>
        public bool Equals(AvatarProviderDescriptor other)
            => other != null
               && ProviderTypeId == other.ProviderTypeId
               && ReferenceEquals(Config, other.Config);

        public override bool Equals(object obj) => Equals(obj as AvatarProviderDescriptor);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (ProviderTypeId != null ? ProviderTypeId.GetHashCode() : 0);
                hash = hash * 31 + (Config != null ? RuntimeHelpers.GetHashCode(Config) : 0);
                return hash;
            }
        }

        public static bool operator ==(AvatarProviderDescriptor a, AvatarProviderDescriptor b)
            => a is null ? b is null : a.Equals(b);
        public static bool operator !=(AvatarProviderDescriptor a, AvatarProviderDescriptor b)
            => !(a == b);
    }
}
