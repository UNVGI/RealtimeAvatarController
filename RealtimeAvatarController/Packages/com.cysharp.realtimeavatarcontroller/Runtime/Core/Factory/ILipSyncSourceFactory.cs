namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// ILipSyncSource の具象インスタンスを生成するファクトリ (将来用)。
    /// キャスト失敗時は <see cref="System.ArgumentException"/> をスローする。
    /// </summary>
    public interface ILipSyncSourceFactory
    {
        /// <summary>
        /// config を元に ILipSyncSource インスタンスを生成する。
        /// config は LipSyncSourceConfigBase 派生型にキャストして使用すること。
        /// キャスト失敗時は <see cref="System.ArgumentException"/> をスローする。
        /// </summary>
        ILipSyncSource Create(LipSyncSourceConfigBase config);
    }
}
