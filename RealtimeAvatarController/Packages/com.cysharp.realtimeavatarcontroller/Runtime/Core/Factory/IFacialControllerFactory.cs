namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IFacialController の具象インスタンスを生成するファクトリ (将来用)。
    /// キャスト失敗時は <see cref="System.ArgumentException"/> をスローする。
    /// </summary>
    public interface IFacialControllerFactory
    {
        /// <summary>
        /// config を元に IFacialController インスタンスを生成する。
        /// config は FacialControllerConfigBase 派生型にキャストして使用すること。
        /// キャスト失敗時は <see cref="System.ArgumentException"/> をスローする。
        /// </summary>
        IFacialController Create(FacialControllerConfigBase config);
    }
}
