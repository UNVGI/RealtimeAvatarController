namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IAvatarProvider の具象インスタンスを生成するファクトリ。
    /// 具象 Factory は IAvatarProviderFactory を実装し、属性ベース自動登録で IProviderRegistry に自己登録する。
    /// </summary>
    public interface IAvatarProviderFactory
    {
        /// <summary>
        /// config を元に IAvatarProvider インスタンスを生成する。
        /// config は ProviderConfigBase 派生型にキャストして使用すること。
        /// キャスト失敗時は <see cref="System.ArgumentException"/> をスローする。
        /// </summary>
        IAvatarProvider Create(ProviderConfigBase config);
    }
}
