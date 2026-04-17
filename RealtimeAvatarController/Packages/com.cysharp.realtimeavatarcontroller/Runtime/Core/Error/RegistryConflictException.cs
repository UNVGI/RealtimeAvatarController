using System;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// Registry への同一 typeId 二重登録時にスローされる例外。
    /// 「最後登録勝ち」は採用しない。デバッグ容易性を最優先とする。
    /// </summary>
    public sealed class RegistryConflictException : Exception
    {
        /// <summary>競合した typeId。</summary>
        public string TypeId { get; }

        /// <summary>Registry の種別名 (例: "IProviderRegistry")。</summary>
        public string RegistryName { get; }

        public RegistryConflictException(string typeId, string registryName)
            : base($"[RegistryConflict] typeId '{typeId}' は {registryName} に既に登録されています。同一 typeId の二重登録は禁止されています。")
        {
            TypeId = typeId;
            RegistryName = registryName;
        }

        public RegistryConflictException(string typeId, string registryName, Exception inner)
            : base($"[RegistryConflict] typeId '{typeId}' は {registryName} に既に登録されています。", inner)
        {
            TypeId = typeId;
            RegistryName = registryName;
        }
    }
}
