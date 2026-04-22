using System;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// MoCap データの Push 型ストリームを提供するソースインターフェース。
    /// MotionStream は受信スレッドから OnNext() で配信され、OnError は発行しない。
    /// 購読側は .ObserveOnMainThread() を使用して Unity メインスレッドで処理すること。
    /// インスタンスのライフサイクルは MoCapSourceRegistry が管理する。
    /// Slot 側から直接 Dispose() を呼び出してはならない。
    /// </summary>
    public interface IMoCapSource : IDisposable
    {
        /// <summary>ソース種別識別子 (例: "VMC", "Custom")</summary>
        string SourceType { get; }

        /// <summary>
        /// 初期化。通信パラメータを格納した Config を渡す。
        /// メインスレッドからの呼び出しを前提とする。
        /// </summary>
        void Initialize(MoCapSourceConfigBase config);

        /// <summary>
        /// Push 型モーションストリーム。
        /// 受信スレッドから Subject.OnNext() で配信される。
        /// 購読側は .ObserveOnMainThread() でメインスレッドに同期すること。
        /// OnError は発行しない。エラーは内部処理しストリームを継続する。
        /// マルチキャスト化 (Publish().RefCount()) は IMoCapSource 実装または MoCapSourceRegistry のラッパーで行う。
        /// </summary>
        IObservable<MotionFrame> MotionStream { get; }

        /// <summary>
        /// シャットダウン。ストリーム停止・リソース解放を行う。
        /// IDisposable.Dispose() と等価。メインスレッドからの呼び出しを前提とする。
        /// </summary>
        void Shutdown();
    }
}
