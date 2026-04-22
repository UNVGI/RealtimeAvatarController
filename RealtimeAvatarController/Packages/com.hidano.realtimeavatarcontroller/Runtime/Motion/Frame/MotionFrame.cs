namespace RealtimeAvatarController.Motion
{
    /// <summary>
    /// 全骨格形式 (Humanoid / Generic 等) 共通の抽象基底型。
    /// IMoCapSource.MotionStream が流すフレーム型として使用する。
    /// <see cref="RealtimeAvatarController.Core.MotionFrame"/> を継承し Core 側の抽象契約と
    /// 構造的互換性を保つ (IObservable&lt;Core.MotionFrame&gt; に継承多態で流せる)。
    /// </summary>
    public abstract class MotionFrame : RealtimeAvatarController.Core.MotionFrame
    {
        /// <summary>
        /// 受信タイムスタンプ (秒単位、App 起動基準の相対値)。
        /// 値: <c>System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency</c> で算出。
        /// 打刻タイミング: 受信ワーカースレッド上でフレーム構築時。
        /// 注意: プロセス間比較不可 (App プロセス起動基準のため)。
        /// </summary>
        public double Timestamp { get; }

        /// <summary>このフレームが表す骨格種別。</summary>
        public abstract SkeletonType SkeletonType { get; }

        /// <summary>
        /// コンストラクタ (派生クラスから呼び出す)。
        /// <paramref name="timestamp"/> は受信スレッド上で取得した Stopwatch ベース値を渡すこと。
        /// 受信ワーカースレッドから安全に呼び出せる。
        /// </summary>
        /// <param name="timestamp">Stopwatch ベースの秒単位 monotonic タイムスタンプ。</param>
        protected MotionFrame(double timestamp)
        {
            Timestamp = timestamp;
        }

        // 将来拡張フィールド (初期版では未実装):
        // public DateTime? WallClock { get; }  // ログ用途の wall clock (初期版では定義しない)
    }
}
