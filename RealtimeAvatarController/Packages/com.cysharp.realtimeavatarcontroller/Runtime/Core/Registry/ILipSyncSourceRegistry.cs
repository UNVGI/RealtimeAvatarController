using System.Collections.Generic;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// ILipSyncSource 具象型の登録・解決を担う Registry (将来用)。
    /// 同一 typeId の二重登録は <see cref="RegistryConflictException"/> をスローする。
    /// </summary>
    public interface ILipSyncSourceRegistry
    {
        /// <summary>
        /// sourceTypeId をキーとして ILipSyncSourceFactory を登録する。
        /// 同一 sourceTypeId が既に登録されている場合は <see cref="RegistryConflictException"/> をスローする。
        /// </summary>
        void Register(string sourceTypeId, ILipSyncSourceFactory factory);

        /// <summary>
        /// LipSyncSourceDescriptor に対応する ILipSyncSource インスタンスを生成して返す。
        /// 未登録 typeId の場合は <see cref="KeyNotFoundException"/> をスローする。
        /// </summary>
        ILipSyncSource Resolve(LipSyncSourceDescriptor descriptor);

        /// <summary>
        /// 登録済みの sourceTypeId 一覧を返す。エディタ UI が利用可能な候補を列挙するために使用する。
        /// </summary>
        IReadOnlyList<string> GetRegisteredTypeIds();
    }
}
