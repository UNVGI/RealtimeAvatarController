namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IMoCapSource の具象インスタンスを生成するファクトリ。
    /// 具象 Factory は IMoCapSourceFactory を実装し、属性ベース自動登録で IMoCapSourceRegistry に自己登録する。
    /// </summary>
    public interface IMoCapSourceFactory
    {
        /// <summary>
        /// config を元に IMoCapSource インスタンスを生成する。
        /// config は MoCapSourceConfigBase 派生型にキャストして使用すること。
        /// キャスト失敗時は <see cref="System.ArgumentException"/> をスローする。
        /// </summary>
        IMoCapSource Create(MoCapSourceConfigBase config);
    }
}
