using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.MoCap.VMC.Internal;
using RealtimeAvatarController.Motion;
using UnityEngine;
using uOSC;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// VmcMessageRouter の OSC アドレスディスパッチ EditMode 単体テスト
    /// (tasks.md タスク 4-1 / design.md §6.2 / requirements.md 要件 5-1, 5-2, 7-2, 10-2)。
    ///
    /// <para>
    /// TDD 先行作成: 本テストファイル作成時点では以下の型は未実装である。
    ///   - <c>RealtimeAvatarController.MoCap.VMC.Internal.VmcMessageRouter</c> (タスク 4-2)
    ///   - <c>RealtimeAvatarController.MoCap.VMC.Internal.VmcFrameBuilder</c> (タスク 5-1)
    /// したがって本ファイルはタスク 4-2・タスク 5-1 の実装完了までコンパイルエラーとなってよい
    /// (tasks.md タスク 4-1 の TDD 方針)。最終的なモック/スタブ整備はタスク 10-1 で行う。
    /// </para>
    ///
    /// <para>
    /// 検証対象 (tasks.md タスク 4-1):
    /// <list type="bullet">
    ///   <item>正常な <c>/VMC/Ext/Bone/Pos</c> アドレスのルーティング
    ///     → <c>VmcFrameBuilder.SetBone</c> が呼ばれる (design.md §6.2)</item>
    ///   <item>正常な <c>/VMC/Ext/Root/Pos</c> アドレスのルーティング
    ///     → <c>VmcFrameBuilder.SetRoot</c> が呼ばれる (design.md §6.2, §7.2)</item>
    ///   <item><c>/VMC/Ext/Blend/Val</c> アドレス
    ///     → 例外をスローせず、フレームビルダへの呼び出しなし (design.md §7.4)</item>
    ///   <item>未知アドレス (<c>/VMC/Ext/Unknown</c>)
    ///     → 例外をスローせず無視される (design.md §6.2)</item>
    ///   <item>uOSC の <c>OscDataHandle</c> (uOSC.Message) に引数が不足している場合
    ///     → 例外をスローせず、<c>PublishError</c> 相当の通知 (errorHandler コールバック) が行われる
    ///     (design.md §8.1, §8.2)</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 本テストでは <c>VmcMessageRouter</c> を以下のシグネチャで構築することを前提とする
    /// (タスク 4-2 実装で確定):
    /// <code>
    /// new VmcMessageRouter(VmcFrameBuilder frameBuilder, Action&lt;Exception&gt; onError);
    /// </code>
    /// また VMC プロトコル上 OSC データは <c>uOSC.Message</c> (address + values) として表現される
    /// (design.md §6.2: uOSC が OSC パースまで担い、router は address と引数リストを解釈する)。
    /// </para>
    /// </summary>
    [TestFixture]
    public class VmcOscParserTests
    {
        private VmcFrameBuilder _frameBuilder;
        private List<Exception> _errors;
        private VmcMessageRouter _router;

        [SetUp]
        public void SetUp()
        {
            _frameBuilder = new VmcFrameBuilder();
            _errors = new List<Exception>();
            _router = new VmcMessageRouter(_frameBuilder, ex => _errors.Add(ex));
        }

        [Test]
        public void Route_BonePosAddress_InvokesSetBone_EnablesFrameFlush()
        {
            // design.md §6.2: /VMC/Ext/Bone/Pos は VmcFrameBuilder.SetBone に転送される。
            // tasks.md タスク 5-1: SetBone 受信後に TryFlush(out frame) が有効フレームを返す簡易実装 (OI-1)。
            var msg = new Message(
                "/VMC/Ext/Bone/Pos",
                "Hips",
                0.0f, 0.0f, 0.0f,          // position
                0.0f, 0.0f, 0.0f, 1.0f);   // rotation (identity)

            Assert.DoesNotThrow(() => _router.Route(msg.address, msg),
                "正常な /VMC/Ext/Bone/Pos の Route は例外をスローしてはならない。");

            Assert.IsTrue(_frameBuilder.TryFlush(out var frame),
                "/VMC/Ext/Bone/Pos 受信後は SetBone が呼ばれ、TryFlush が有効フレームを返すべき (design.md §6.3)。");
            Assert.IsNotNull(frame,
                "TryFlush が true を返したときに frame は非 null であるべき。");
            Assert.IsEmpty(_errors,
                "正常系では errorHandler は呼ばれてはならない。");
        }

        [Test]
        public void Route_RootPosAddress_InvokesSetRoot_ReflectsInFlushedFrame()
        {
            // design.md §7.2: /VMC/Ext/Root/Pos の引数は (name, px, py, pz, qx, qy, qz, qw)。
            // Root は VmcFrameBuilder 内部状態 (rootPosition/rootRotation) を更新するため、
            // 直後に Bone を受信してフレームをフラッシュし、Root 値が反映されていることを確認する。
            var rootMsg = new Message(
                "/VMC/Ext/Root/Pos",
                "root",
                1.0f, 2.0f, 3.0f,          // position
                0.0f, 0.0f, 0.0f, 1.0f);   // rotation (identity)
            var boneMsg = new Message(
                "/VMC/Ext/Bone/Pos",
                "Hips",
                0.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f);

            Assert.DoesNotThrow(() => _router.Route(rootMsg.address, rootMsg),
                "正常な /VMC/Ext/Root/Pos の Route は例外をスローしてはならない。");
            Assert.DoesNotThrow(() => _router.Route(boneMsg.address, boneMsg),
                "正常な /VMC/Ext/Bone/Pos の Route は例外をスローしてはならない。");

            Assert.IsTrue(_frameBuilder.TryFlush(out var frame),
                "Root + Bone を受信した後は TryFlush が有効フレームを返すべき。");
            Assert.AreEqual(
                new Vector3(1.0f, 2.0f, 3.0f),
                frame.RootPosition,
                "SetRoot が呼ばれた結果、フラッシュしたフレームの RootPosition が Root メッセージの位置と一致すべき (design.md §7.2)。");
            Assert.IsEmpty(_errors,
                "正常系では errorHandler は呼ばれてはならない。");
        }

        [Test]
        public void Route_BlendValAddress_DoesNotThrow_NoFrameBuilderSideEffect()
        {
            // design.md §7.4: 初期版では /VMC/Ext/Blend/Val は受信のみ・変換スキップ。
            // tasks.md タスク 4-1: 「例外をスローせず、フレームビルダへの呼び出しなし」。
            var msg = new Message(
                "/VMC/Ext/Blend/Val",
                "A",
                0.5f);

            Assert.DoesNotThrow(() => _router.Route(msg.address, msg),
                "/VMC/Ext/Blend/Val の Route は例外をスローしてはならない (初期版スキップ方針)。");

            Assert.IsFalse(_frameBuilder.TryFlush(out _),
                "/VMC/Ext/Blend/Val のみ受信した状態では Bone 未受信のため TryFlush は false を返すべき " +
                "(design.md §7.3 / tasks.md タスク 5-1: 無効フレーム判定)。");
            Assert.IsEmpty(_errors,
                "Blend/Val は正常スキップのため errorHandler は呼ばれてはならない。");
        }

        [Test]
        public void Route_UnknownAddress_DoesNotThrow_Ignored()
        {
            // design.md §6.2: default (未知アドレス) は無視。
            // tasks.md タスク 4-1: 「例外をスローせず無視される」。
            var msg = new Message(
                "/VMC/Ext/Unknown",
                "payload",
                42);

            Assert.DoesNotThrow(() => _router.Route(msg.address, msg),
                "未知アドレスの Route は例外をスローしてはならない。");

            Assert.IsFalse(_frameBuilder.TryFlush(out _),
                "未知アドレスのみ受信した状態では Bone 未受信のため TryFlush は false を返すべき。");
            Assert.IsEmpty(_errors,
                "未知アドレスは無視されるべきで errorHandler は呼ばれてはならない (design.md §6.2)。");
        }

        [Test]
        public void Route_BonePos_WithInsufficientArguments_DoesNotThrow_InvokesErrorHandler()
        {
            // design.md §8.1, §8.2: OSC パースエラー / 引数不足は Route 呼び出し元には例外をスローせず、
            // PublishError 相当の errorHandler (= VmcOscAdapter.PublishError) に通知される。
            // tasks.md タスク 4-1: 「例外をスローせず、PublishError 相当の通知が行われる」。
            var msg = new Message(
                "/VMC/Ext/Bone/Pos",
                "Hips"); // 本来 8 引数必要 (name + 3 pos + 4 rot) だが 1 引数のみ

            Assert.DoesNotThrow(() => _router.Route(msg.address, msg),
                "引数不足時でも Route は例外をスローしてはならない (design.md §8.1)。");

            Assert.IsNotEmpty(_errors,
                "引数不足時は PublishError 相当として errorHandler に例外が通知されるべき (design.md §8.2)。");
        }
    }
}
