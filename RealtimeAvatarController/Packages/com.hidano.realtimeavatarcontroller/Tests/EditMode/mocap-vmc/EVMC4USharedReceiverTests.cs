using EVMC4U;
using NUnit.Framework;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// <see cref="EVMC4USharedReceiver"/> の参照カウント / 単一性 / 静的リセットを検証する EditMode テスト
    /// (tasks.md タスク 3.1 / design.md §4.3 / requirements.md 要件 2.1, 2.2, 2.3, 2.4, 4.4)。
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="EVMC4USharedReceiver.EnsureInstance"/> 2 回呼出で refCount=2, 同一 ExternalReceiver を返すこと
    ///   - <see cref="EVMC4USharedReceiver.Release"/> 2 回呼出で GameObject が破棄されること
    ///   - EnsureInstance 返却時点で Root*Synchronize=false / Model=null が確定していること
    ///   - <see cref="EVMC4USharedReceiver.ResetForTest"/> 呼出後に refCount=0 / instance=null にリセットされること
    /// </para>
    ///
    /// <para>
    /// 本テストは実装完了前は赤 (コンパイル失敗 or Assertion 失敗) を許容する
    /// (tasks.md タスク 3.1 の観測可能な完了条件)。
    /// </para>
    /// </summary>
    [TestFixture]
    public class EVMC4USharedReceiverTests
    {
        [SetUp]
        public void SetUp()
        {
            EVMC4USharedReceiver.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            EVMC4USharedReceiver.ResetForTest();
        }

        [Test]
        public void EnsureInstance_FirstCall_CreatesSingletonAndReceiver()
        {
            var shared = EVMC4USharedReceiver.EnsureInstance();

            Assert.That(shared, Is.Not.Null,
                "EnsureInstance() は常に非 null のインスタンスを返すべき (task 3.2)。");
            Assert.That(shared.Receiver, Is.Not.Null,
                "Receiver プロパティは ExternalReceiver を公開すべき (task 3.2 / design.md §4.3)。");
            Assert.That(shared.RefCountForTest, Is.EqualTo(1),
                "初回 EnsureInstance() 後は refCount が 1 になるべき。");
        }

        [Test]
        public void EnsureInstance_CalledTwice_IncrementsRefCount_AndReturnsSameReceiver()
        {
            var firstShared = EVMC4USharedReceiver.EnsureInstance();
            var firstReceiver = firstShared.Receiver;

            var secondShared = EVMC4USharedReceiver.EnsureInstance();

            Assert.That(secondShared, Is.SameAs(firstShared),
                "EnsureInstance() は同一プロセス内で同一の EVMC4USharedReceiver を返すべき (要件 2.1)。");
            Assert.That(secondShared.Receiver, Is.SameAs(firstReceiver),
                "二回目の EnsureInstance() も同一 ExternalReceiver インスタンスを返すべき (要件 2.2)。");
            Assert.That(secondShared.RefCountForTest, Is.EqualTo(2),
                "2 回 EnsureInstance() した時点で refCount=2 になるべき (task 3.1)。");
        }

        [Test]
        public void EnsureInstance_InitializesReceiver_WithRootSynchronizeFalse()
        {
            var shared = EVMC4USharedReceiver.EnsureInstance();

            Assert.That(shared.Receiver.RootPositionSynchronize, Is.False,
                "EnsureInstance 返却時点で RootPositionSynchronize=false が強制されるべき (要件 2.4 / design.md §4.3)。");
            Assert.That(shared.Receiver.RootRotationSynchronize, Is.False,
                "EnsureInstance 返却時点で RootRotationSynchronize=false が強制されるべき (要件 2.4 / design.md §4.3)。");
            Assert.That(shared.Receiver.Model, Is.Null,
                "EnsureInstance 返却時点で Receiver.Model=null が強制されるべき (要件 2.4)。");
        }

        [Test]
        public void Release_CalledTwice_DestroysGameObject()
        {
            var shared = EVMC4USharedReceiver.EnsureInstance();
            EVMC4USharedReceiver.EnsureInstance();
            var hostGo = shared.gameObject;

            shared.Release();
            Assert.That(shared.RefCountForTest, Is.EqualTo(1),
                "1 回目の Release で refCount=1 に減るべき (要件 2.3)。");
            Assert.That(hostGo == null, Is.False,
                "refCount>0 の間は GameObject を破棄してはいけない (要件 2.2)。");

            shared.Release();

            Assert.That(EVMC4USharedReceiver.InstanceForTest == null, Is.True,
                "refCount が 0 になった時点で singleton への静的参照が null に戻るべき (task 3.1 / 要件 2.3)。");
            Assert.That(hostGo == null, Is.True,
                "Release を refCount 分だけ呼んだ後、EditMode では GameObject が即座に破棄されているべき (task 3.1 / 要件 2.3)。");
        }

        [Test]
        public void ResetForTest_ClearsStaticState()
        {
            var shared = EVMC4USharedReceiver.EnsureInstance();
            EVMC4USharedReceiver.EnsureInstance();
            Assert.That(shared.RefCountForTest, Is.EqualTo(2),
                "ResetForTest 前は refCount=2 を保持しているべき。");

            EVMC4USharedReceiver.ResetForTest();

            Assert.That(EVMC4USharedReceiver.InstanceForTest == null, Is.True,
                "ResetForTest 後は static な singleton 参照が null になるべき (task 3.3 / 要件 2.1 / 6.4)。");
            Assert.That(EVMC4USharedReceiver.RefCountStaticForTest, Is.EqualTo(0),
                "ResetForTest 後は静的 refCount が 0 になるべき (task 3.3 / 要件 6.4)。");
        }

        [Test]
        public void ResetForTest_AfterRelease_SupportsFreshEnsureInstance()
        {
            var first = EVMC4USharedReceiver.EnsureInstance();
            var firstGo = first.gameObject;
            first.Release();

            EVMC4USharedReceiver.ResetForTest();

            var second = EVMC4USharedReceiver.EnsureInstance();

            Assert.That(second, Is.Not.Null,
                "ResetForTest 後に EnsureInstance() が再び新しいインスタンスを生成できるべき (task 3.4 / 要件 6.4)。");
            Assert.That(second.gameObject == firstGo, Is.False,
                "ResetForTest 後は古い GameObject への参照を漏らさず新規生成するべき。");
            Assert.That(second.RefCountForTest, Is.EqualTo(1),
                "ResetForTest 後の初回 EnsureInstance() で refCount=1 になるべき。");
        }
    }
}
