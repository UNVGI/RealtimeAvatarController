namespace RealtimeAvatarController.Motion
{
    /// <summary>
    /// Generic 骨格向けモーションフレームの将来実装向けプレースホルダー。
    /// 初期段階では具象フィールドを定義しない。
    /// 具象実装は本 Spec (motion-pipeline) のスコープ外であり、将来の Generic Spec が担う。
    /// </summary>
    public abstract class GenericMotionFrame : MotionFrame
    {
        /// <inheritdoc/>
        public override SkeletonType SkeletonType => SkeletonType.Generic;

        /// <summary>
        /// コンストラクタ (派生クラスから呼び出す)。
        /// </summary>
        /// <param name="timestamp">Stopwatch ベースの秒単位 monotonic タイムスタンプ。</param>
        protected GenericMotionFrame(double timestamp) : base(timestamp) { }

        // 将来実装予定フィールド (初期版では未実装):
        // public TransformData[] Bones { get; }  // 各ボーンの位置・回転・スケール
    }
}
