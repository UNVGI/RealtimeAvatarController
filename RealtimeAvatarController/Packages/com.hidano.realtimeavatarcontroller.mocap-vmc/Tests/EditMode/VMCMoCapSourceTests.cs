using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// <see cref="VMCMoCapSource"/> の基本契約を検証する EditMode テスト
    /// (tasks.md タスク 4.1 / design.md §4.2, §5.1 / requirements.md 要件 1.1, 1.2, 1.3, 1.4, 1.5, 5.3, 7.5)。
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="IMoCapSource.SourceType"/> が "VMC" を返すこと
    ///   - <see cref="IMoCapSource.Initialize"/> の二重呼び出しで <see cref="InvalidOperationException"/>
    ///   - 別の <see cref="MoCapSourceConfigBase"/> 派生型を渡すと <see cref="ArgumentException"/> (型名を含む)
    ///   - port=0/1024/65536 で <see cref="ArgumentOutOfRangeException"/>
    ///   - <see cref="IDisposable.Dispose"/> 後の <see cref="IMoCapSource.Shutdown"/> は no-op
    ///   - <see cref="IMoCapSource.MotionStream"/> が <see cref="IObservable{T}"/> で公開される
    /// </para>
    ///
    /// <para>
    /// 本テストは実装完了前は赤 (コンパイル失敗 or Assertion 失敗) を許容する
    /// (tasks.md タスク 4.1 の観測可能な完了条件)。
    /// </para>
    /// </summary>
    [TestFixture]
    public class VMCMoCapSourceTests
    {
        private const int ValidTestPort = 49501;
        private const int ExceptionTestPort = 49503;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            VMCSharedReceiver.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            VMCSharedReceiver.ResetForTest();
            RegistryLocator.ResetForTest();
        }

        // --- SourceType (要件 1.3) ---

        [Test]
        public void SourceType_ReturnsVmc()
        {
            var source = CreateSource();

            Assert.That(source.SourceType, Is.EqualTo("VMC"),
                "VMCMoCapSource.SourceType は常に \"VMC\" を返すべき (要件 1.3)。");
        }

        // --- MotionStream 型 (要件 1.4) ---

        [Test]
        public void MotionStream_IsObservableOfMotionFrame()
        {
            var source = CreateSource();

            Assert.That(source.MotionStream, Is.Not.Null,
                "Initialize 前でも MotionStream は null を返してはならない (要件 1.9)。");
            Assert.That(source.MotionStream, Is.InstanceOf<IObservable<MotionFrame>>(),
                "MotionStream は IObservable<MotionFrame> を実装するべき (要件 1.4)。");
        }

        // --- Initialize: config 型不一致 (要件 1.5) ---

        [Test]
        public void Initialize_WithOtherConfigType_ThrowsArgumentException_WithTypeNameInMessage()
        {
            var source = CreateSource();
            var other = ScriptableObject.CreateInstance<OtherMoCapSourceConfig>();
            try
            {
                var ex = Assert.Throws<ArgumentException>(() => source.Initialize(other));
                StringAssert.Contains(nameof(OtherMoCapSourceConfig), ex.Message,
                    "型不一致時の ArgumentException には受け取った型名が含まれるべき (要件 1.5)。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void Initialize_WithNullConfig_ThrowsArgumentException()
        {
            var source = CreateSource();

            Assert.Throws<ArgumentException>(() => source.Initialize(null),
                "null を渡された場合は ArgumentException をスローすべき (要件 1.5)。");
        }

        // --- Initialize: port 範囲 (要件 5.3) ---

        [Test]
        public void Initialize_WithPortZero_ThrowsArgumentOutOfRangeException()
        {
            var source = CreateSource();
            var config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            try
            {
                config.port = 0;
                Assert.Throws<ArgumentOutOfRangeException>(() => source.Initialize(config),
                    "port=0 は範囲外として ArgumentOutOfRangeException をスローすべき (要件 5.3)。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Initialize_WithPort1024_ThrowsArgumentOutOfRangeException()
        {
            var source = CreateSource();
            var config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            try
            {
                config.port = 1024;
                Assert.Throws<ArgumentOutOfRangeException>(() => source.Initialize(config),
                    "port=1024 は範囲外として ArgumentOutOfRangeException をスローすべき (要件 5.3)。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Initialize_WithPort65536_ThrowsArgumentOutOfRangeException()
        {
            var source = CreateSource();
            var config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            try
            {
                config.port = 65536;
                Assert.Throws<ArgumentOutOfRangeException>(() => source.Initialize(config),
                    "port=65536 は範囲外として ArgumentOutOfRangeException をスローすべき (要件 5.3)。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        // --- Initialize: 二重呼び出し (要件 1.5 / 7.5) ---

        [Test]
        public void Initialize_CalledTwice_ThrowsInvalidOperationException()
        {
            var source = CreateSource();
            var config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            try
            {
                config.port = ValidTestPort;
                source.Initialize(config);

                Assert.Throws<InvalidOperationException>(() => source.Initialize(config),
                    "二回目の Initialize は InvalidOperationException をスローすべき (要件 7.5)。");
            }
            finally
            {
                source.Dispose();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        // --- Dispose / Shutdown 冪等性 (要件 1.2) ---

        [Test]
        public void Shutdown_AfterDispose_IsNoOp()
        {
            var source = CreateSource();

            source.Dispose();
            Assert.DoesNotThrow(() => source.Shutdown(),
                "Dispose 後の Shutdown は no-op であり例外を投げてはならない (要件 1.2)。");
        }

        [Test]
        public void Dispose_CalledTwice_IsNoOp()
        {
            var source = CreateSource();

            source.Dispose();
            Assert.DoesNotThrow(() => source.Dispose(),
                "二回目の Dispose は no-op であり例外を投げてはならない (要件 1.2)。");
        }

        // --- ヘルパー ---

        // --- Task 4.3: Tick error propagation / completion ---

        [Test]
        public void TickException_IsForwardedToSlotErrorChannel_AndDoesNotCallMotionStreamOnError()
        {
            var errorChannel = new SpySlotErrorChannel();
            var source = new VMCMoCapSource("slot-vmc-tick", errorChannel);
            var config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            var recorder = new FrameRecorder();
            var subscription = source.MotionStream.Subscribe(recorder);
            try
            {
                config.port = ExceptionTestPort;
                source.Initialize(config);

                var exception = new InvalidOperationException("simulated VMC Tick failure");
                var before = DateTime.UtcNow;
                source.ThrowOnNextTickForTest(exception);
                source.ForceTickForTest();
                var after = DateTime.UtcNow;

                Assert.That(errorChannel.Published.Count, Is.EqualTo(1),
                    "Tick exception must be published once through ISlotErrorChannel.");
                var error = errorChannel.Published[0];
                Assert.That(error.SlotId, Is.EqualTo("slot-vmc-tick"));
                Assert.That(error.Category, Is.EqualTo(SlotErrorCategory.VmcReceive));
                Assert.That(error.Exception, Is.SameAs(exception));
                Assert.That(error.Timestamp, Is.InRange(before, after));
                Assert.That(recorder.Errors, Is.Empty,
                    "Tick exceptions must not be forwarded to MotionStream.OnError.");
                Assert.That(recorder.Frames, Is.Empty);
            }
            finally
            {
                subscription.Dispose();
                source.Dispose();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Shutdown_NotifiesMotionStreamCompletion()
        {
            var source = CreateSource();
            var recorder = new FrameRecorder();
            var subscription = source.MotionStream.Subscribe(recorder);
            try
            {
                source.Shutdown();

                Assert.That(recorder.Completed, Is.True,
                    "Shutdown must notify MotionStream subscribers with OnCompleted.");
                Assert.That(recorder.Errors, Is.Empty,
                    "Shutdown must not invoke MotionStream.OnError.");
            }
            finally
            {
                subscription.Dispose();
                source.Dispose();
            }
        }

        // --- Helpers ---

        private static VMCMoCapSource CreateSource()
        {
            return new VMCMoCapSource(
                slotId: string.Empty,
                errorChannel: RegistryLocator.ErrorChannel);
        }

        /// <summary>
        /// キャスト失敗経路検証用のダミー <see cref="MoCapSourceConfigBase"/> 派生型。
        /// </summary>
        private sealed class OtherMoCapSourceConfig : MoCapSourceConfigBase
        {
        }

        private sealed class SpySlotErrorChannel : ISlotErrorChannel
        {
            public List<SlotError> Published { get; } = new List<SlotError>();

            public IObservable<SlotError> Errors => NoOpObservable.Instance;

            public void Publish(SlotError error)
            {
                Published.Add(error);
            }

            private sealed class NoOpObservable : IObservable<SlotError>
            {
                public static readonly NoOpObservable Instance = new NoOpObservable();

                public IDisposable Subscribe(IObserver<SlotError> observer) => NoOpDisposable.Instance;
            }

            private sealed class NoOpDisposable : IDisposable
            {
                public static readonly NoOpDisposable Instance = new NoOpDisposable();

                public void Dispose()
                {
                }
            }
        }

        private sealed class FrameRecorder : IObserver<MotionFrame>
        {
            public List<MotionFrame> Frames { get; } = new List<MotionFrame>();

            public List<Exception> Errors { get; } = new List<Exception>();

            public bool Completed { get; private set; }

            public void OnNext(MotionFrame value) => Frames.Add(value);

            public void OnError(Exception error) => Errors.Add(error);

            public void OnCompleted() => Completed = true;
        }
    }
}
