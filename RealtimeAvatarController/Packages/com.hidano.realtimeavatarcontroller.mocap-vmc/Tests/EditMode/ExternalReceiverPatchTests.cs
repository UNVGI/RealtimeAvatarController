using System.Reflection;
using EVMC4U;
using NUnit.Framework;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// ExternalReceiver (Assets/EVMC4U/ExternalReceiver.cs) への local patch 検証用 EditMode テスト
    /// (tasks.md タスク 2.1 / design.md §6 / requirements.md 要件 1.8, 2.4, 2.5, 3.1, 3.4, 3.6, 10.5, 12.3, 12.7)。
    ///
    /// <para>
    /// 検証対象:
    ///   - Model=null のままでも Bone/Root Dictionary 蓄積が継続する (task 2.2)
    ///   - GetBoneRotationsView / GetBonePositionsView / IsShutdown 読取アクセサ (task 2.3)
    ///   - /VMC/Ext/Root/Pos 受信時に LatestRootLocalPosition / LatestRootLocalRotation が更新される (task 2.4)
    ///   - InjectBoneRotationForTest / InjectBonePositionForTest テスト用 Setter (task 2.5)
    /// </para>
    ///
    /// <para>
    /// 本テストは実装完了前は赤 (コンパイル失敗 or Assertion 失敗) を許容する
    /// (tasks.md タスク 2.1 の観測可能な完了条件)。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ExternalReceiverPatchTests
    {
        private GameObject _receiverGo;
        private ExternalReceiver _receiver;
        private GameObject _modelGo;

        [SetUp]
        public void SetUp()
        {
            _receiverGo = new GameObject("ExternalReceiverPatchTests.Receiver");
            _receiver = _receiverGo.AddComponent<ExternalReceiver>();
            _receiver.Model = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_receiverGo != null)
            {
                Object.DestroyImmediate(_receiverGo);
                _receiverGo = null;
                _receiver = null;
            }
            if (_modelGo != null)
            {
                Object.DestroyImmediate(_modelGo);
                _modelGo = null;
            }
        }

        // --- task 2.3 / 2.5: 読取アクセサとテスト用 Setter ---

        [Test]
        public void InjectBoneRotationForTest_WhenModelNull_AccumulatesInBoneRotationsView()
        {
            var rot = Quaternion.Euler(10f, 20f, 30f);

            _receiver.InjectBoneRotationForTest(HumanBodyBones.LeftHand, rot);

            var view = _receiver.GetBoneRotationsView();
            Assert.That(view, Is.Not.Null, "GetBoneRotationsView() は null を返してはいけない (task 2.3)。");
            Assert.That(view.ContainsKey(HumanBodyBones.LeftHand), Is.True,
                "Model=null でも InjectBoneRotationForTest で書き込んだ bone が View に反映されるべき (task 2.2 / 2.5)。");
            Assert.That(view[HumanBodyBones.LeftHand], Is.EqualTo(rot),
                "注入した回転値が View に保持されるべき。");
        }

        [Test]
        public void InjectBonePositionForTest_WhenModelNull_AccumulatesInBonePositionsView()
        {
            var pos = new Vector3(1.1f, 2.2f, 3.3f);

            _receiver.InjectBonePositionForTest(HumanBodyBones.LeftHand, pos);

            var view = _receiver.GetBonePositionsView();
            Assert.That(view, Is.Not.Null, "GetBonePositionsView() は null を返してはいけない (task 2.3)。");
            Assert.That(view.ContainsKey(HumanBodyBones.LeftHand), Is.True,
                "Model=null でも InjectBonePositionForTest で書き込んだ bone が View に反映されるべき (task 2.2 / 2.5)。");
            Assert.That(view[HumanBodyBones.LeftHand], Is.EqualTo(pos),
                "注入した位置値が View に保持されるべき。");
        }

        [Test]
        public void IsShutdown_Default_ReturnsFalse()
        {
            Assert.That(_receiver.IsShutdown, Is.False,
                "ExternalReceiver の IsShutdown の既定値は false であるべき (task 2.3)。");
        }

        [Test]
        public void GetBoneRotationsView_Default_IsEmpty()
        {
            var view = _receiver.GetBoneRotationsView();

            Assert.That(view, Is.Not.Null);
            Assert.That(view.Count, Is.EqualTo(0),
                "受信前の GetBoneRotationsView() は空の Dictionary を返すべき (task 2.3)。");
        }

        [Test]
        public void LatestRootLocalRotation_Default_IsIdentity()
        {
            Assert.That(_receiver.LatestRootLocalRotation, Is.EqualTo(Quaternion.identity),
                "LatestRootLocalRotation の既定値は Quaternion.identity であるべき (task 2.4 / design.md §6.2)。");
        }

        [Test]
        public void LatestRootLocalPosition_Default_IsZero()
        {
            Assert.That(_receiver.LatestRootLocalPosition, Is.EqualTo(Vector3.zero),
                "LatestRootLocalPosition の既定値は Vector3.zero であるべき (task 2.4)。");
        }

        // --- task 2.2: Model=null ガード緩和 (ProcessMessage 経由) ---

        [Test]
        public void ProcessMessage_BonePos_WhenModelNull_UpdatesBoneRotationsView()
        {
            var message = new uOSC.Message(
                "/VMC/Ext/Bone/Pos",
                "RightHand",
                0f, 0f, 0f,       // pos.x/y/z
                0.1f, 0.2f, 0.3f, 0.9f); // rot.x/y/z/w

            InvokeProcessMessage(_receiver, ref message);

            var view = _receiver.GetBoneRotationsView();
            Assert.That(view.ContainsKey(HumanBodyBones.RightHand), Is.True,
                "Model=null でも /VMC/Ext/Bone/Pos を受信すれば Bone Dictionary に蓄積されるべき (task 2.2)。");
        }

        [Test]
        public void ProcessMessage_BonePos_WhenModelSet_StillUpdatesBoneRotationsView()
        {
            _modelGo = new GameObject("ExternalReceiverPatchTests.Model");
            _receiver.Model = _modelGo;
            // RootPositionTransform / RootRotationTransform は Model が非 null なら
            // ProcessMessage 内で Model.transform に自動初期化される。

            var message = new uOSC.Message(
                "/VMC/Ext/Bone/Pos",
                "LeftFoot",
                0f, 0f, 0f,
                0f, 0f, 0f, 1f);

            InvokeProcessMessage(_receiver, ref message);

            var view = _receiver.GetBoneRotationsView();
            Assert.That(view.ContainsKey(HumanBodyBones.LeftFoot), Is.True,
                "Model 有りの場合も従来通り Bone Dictionary 蓄積が行われるべき (回帰 / task 2.2)。");
        }

        // --- task 2.4: Root キャッシュ ---

        [Test]
        public void ProcessMessage_RootPos_WhenModelNull_CachesLatestRootValues()
        {
            // RootPositionSynchronize / RootRotationSynchronize は default=true だが、
            // Model=null のため Transform 書込パスはスキップされる前提 (task 2.2 / design §6.1)。
            // 一方、LatestRootLocalPosition / LatestRootLocalRotation は常にキャッシュされる (task 2.4)。
            var expectedPos = new Vector3(1.5f, 2.5f, 3.5f);
            var expectedRot = new Quaternion(0.1f, 0.2f, 0.3f, 0.9f);

            var message = new uOSC.Message(
                "/VMC/Ext/Root/Pos",
                "root",
                expectedPos.x, expectedPos.y, expectedPos.z,
                expectedRot.x, expectedRot.y, expectedRot.z, expectedRot.w);

            InvokeProcessMessage(_receiver, ref message);

            Assert.That(_receiver.LatestRootLocalPosition, Is.EqualTo(expectedPos),
                "/VMC/Ext/Root/Pos 受信時に LatestRootLocalPosition が更新されるべき (task 2.4)。");
            Assert.That(_receiver.LatestRootLocalRotation, Is.EqualTo(expectedRot),
                "/VMC/Ext/Root/Pos 受信時に LatestRootLocalRotation が更新されるべき (task 2.4)。");
        }

        // --- ヘルパー ---

        /// <summary>
        /// private <c>ExternalReceiver.ProcessMessage(ref uOSC.Message)</c> を Reflection 経由で呼び出す。
        /// EditMode テストでは OnDataReceived/MessageDaisyChain 経路が Start() 未実行で動かないため、
        /// ProcessMessage を直接呼び出して受信経路を単体検証する。
        /// </summary>
        private static void InvokeProcessMessage(ExternalReceiver receiver, ref uOSC.Message message)
        {
            var method = typeof(ExternalReceiver).GetMethod(
                "ProcessMessage",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(method, Is.Not.Null,
                "ExternalReceiver.ProcessMessage(ref uOSC.Message) が存在するべき。");

            var args = new object[] { message };
            try
            {
                method.Invoke(receiver, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
            message = (uOSC.Message)args[0];
        }
    }
}
