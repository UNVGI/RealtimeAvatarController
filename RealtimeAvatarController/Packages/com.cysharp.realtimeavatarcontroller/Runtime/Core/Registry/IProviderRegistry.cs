using System.Collections.Generic;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IAvatarProvider 具象型の登録・解決・候補列挙を担う Registry。
    /// 同一 typeId の二重登録は <see cref="RegistryConflictException"/> をスローする (上書き禁止)。
    /// </summary>
    public interface IProviderRegistry
    {
        /// <summary>
        /// providerTypeId をキーとして IAvatarProviderFactory を登録する。
        /// 同一 providerTypeId が既に登録されている場合は <see cref="RegistryConflictException"/> をスローする。
        /// </summary>
        void Register(string providerTypeId, IAvatarProviderFactory factory);

        /// <summary>
        /// AvatarProviderDescriptor に対応する IAvatarProvider インスタンスを生成して返す。
        /// 未登録 typeId の場合は <see cref="KeyNotFoundException"/> をスローする。
        /// </summary>
        IAvatarProvider Resolve(AvatarProviderDescriptor descriptor);

        /// <summary>
        /// 登録済みの providerTypeId 一覧を返す。エディタ UI が利用可能な候補を列挙するために使用する。
        /// </summary>
        IReadOnlyList<string> GetRegisteredTypeIds();
    }
}
