using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;

namespace RealtimeAvatarController.Motion.Tests.Applier
{
    /// <summary>
    /// Weight 二値判定の EditMode 単体テスト (tasks.md §6-4 / design.md §6.1, §6.2)。
    ///
    /// <para>
    /// テスト対象: SlotManager の LateUpdate での Weight クランプ・Apply 呼び出しロジック
    /// (design.md §6.2 のサンプルコード相当)。
    /// </para>
    /// <para>
    /// SlotManager は <c>RealtimeAvatarController.Core</c> 側に存在するが、本テストは
    /// motion-pipeline 側の <see cref="IMotionApplier"/> 契約 (Applier 内部クランプ禁止 / 初期版は
    /// 0.0・1.0 の二値) を検証する。よって §6.2 の呼び出しロジック (Mathf.Clamp01 → 0.0 判定 →
    /// Apply) を本テスト内の静的ヘルパで再現し、モック Applier で観測する。
    /// </para>
    /// テスト観点:
    ///   - weight == 0.0 で <see cref="IMotionApplier.Apply"/> が呼ばれない
    ///   - weight == 1.0 で Apply が呼ばれる
    ///   - 範囲外 (1.5) は SlotManager 側で Clamp01 し 1.0 で Apply が呼ばれる
    ///   - 範囲外 (-0.5) は SlotManager 側で Clamp01 し 0.0 判定でスキップされる
    ///   - <see cref="IMotionApplier.Apply"/> 実装は内部クランプを行わない (渡された値が
    ///     そのまま伝搬する / doccomment 遵守確認)
    /// Requirements: Req 5 AC4, Req 14
    /// </summary>
    [TestFixture]
    public class WeightTests
    {
        private const int HumanoidMuscleCount = 95;

        private static HumanoidMotionFrame CreateValidFrame()
            => new HumanoidMotionFrame(
                timestamp: 1.0,
                muscles: new float[HumanoidMuscleCount],
                rootPosition: Vector3.zero,
                rootRotation: Quaternion.identity);

        /// <summary>
        /// design.md §6.2 の SlotManager 呼び出しロジックを再現するヘルパ。
        /// <c>Mathf.Clamp01</c> 済みの値のみが Applier に渡される。
        /// </summary>
        private static void InvokeSlotManagerLikeLateUpdate(
            IMotionApplier applier, MotionFrame frame, SlotSettings settings)
        {
            float clampedWeight = Mathf.Clamp01(settings.weight);
            if (clampedWeight == 0f)
            {
                // weight == 0.0 (またはクランプ後 0.0) の場合は Apply を呼び出さず前フレームポーズを維持する。
                return;
            }
            applier.Apply(frame, clampedWeight, settings);
        }

        private SlotSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SlotSettings>();
            _settings.slotId = "weight-test-slot";
            _settings.displayName = "Weight Test Slot";
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null)
            {
                Object.DestroyImmediate(_settings);
                _settings = null;
            }
        }

        [Test]
        public void Weight_Zero_SkipsApply()
        {
            var applier = new RecordingMotionApplier();
            _settings.weight = 0.0f;
            var frame = CreateValidFrame();

            InvokeSlotManagerLikeLateUpdate(applier, frame, _settings);

            Assert.That(applier.ApplyCallCount, Is.EqualTo(0),
                "weight == 0.0 のとき SlotManager は Apply を呼び出してはならない (design.md §6.2)");
        }

        [Test]
        public void Weight_One_CallsApply()
        {
            var applier = new RecordingMotionApplier();
            _settings.weight = 1.0f;
            var frame = CreateValidFrame();

            InvokeSlotManagerLikeLateUpdate(applier, frame, _settings);

            Assert.That(applier.ApplyCallCount, Is.EqualTo(1),
                "weight == 1.0 のとき Apply は 1 回呼び出される");
            Assert.That(applier.LastWeight, Is.EqualTo(1.0f),
                "SlotManager はクランプ済みの weight を Applier に渡す");
            Assert.That(applier.LastFrame, Is.SameAs(frame),
                "受信した frame がそのまま Applier に渡されること");
            Assert.That(applier.LastSettings, Is.SameAs(_settings),
                "SlotSettings がそのまま Applier に渡されること");
        }

        [Test]
        public void Weight_OutOfRange_Positive_ClampsToOne()
        {
            var applier = new RecordingMotionApplier();
            _settings.weight = 1.5f;
            var frame = CreateValidFrame();

            InvokeSlotManagerLikeLateUpdate(applier, frame, _settings);

            Assert.That(applier.ApplyCallCount, Is.EqualTo(1),
                "範囲外 (1.5) は SlotManager 側で 1.0 にクランプされ Apply が呼ばれる");
            Assert.That(applier.LastWeight, Is.EqualTo(1.0f),
                "Applier にはクランプ済みの 1.0 が渡されること (二重クランプ禁止)");
        }

        [Test]
        public void Weight_OutOfRange_Negative_ClampsToZero()
        {
            var applier = new RecordingMotionApplier();
            _settings.weight = -0.5f;
            var frame = CreateValidFrame();

            InvokeSlotManagerLikeLateUpdate(applier, frame, _settings);

            Assert.That(applier.ApplyCallCount, Is.EqualTo(0),
                "範囲外 (-0.5) は SlotManager 側で 0.0 にクランプされ Apply はスキップされる");
        }

        [Test]
        public void Apply_DoesNotClampInternally()
        {
            // Applier 実装は渡された weight をそのまま観測するだけで、内部でクランプしてはならない。
            // 二重クランプ防止 (design.md §6.2 / IMotionApplier doccomment) の契約を検証する。
            var applier = new RecordingMotionApplier();
            var frame = CreateValidFrame();

            applier.Apply(frame, weight: 1.5f, settings: _settings);
            applier.Apply(frame, weight: -0.5f, settings: _settings);
            applier.Apply(frame, weight: 0.0f, settings: _settings);
            applier.Apply(frame, weight: 1.0f, settings: _settings);

            Assert.That(applier.ReceivedWeights, Is.EqualTo(new[] { 1.5f, -0.5f, 0.0f, 1.0f }),
                "IMotionApplier 実装は渡された weight を改変せず観測する (doccomment 遵守)");
        }

        /// <summary>
        /// テスト用 <see cref="IMotionApplier"/> モック。
        /// Apply 呼び出し回数と直近の引数、および受信 weight の履歴を記録する。
        /// 内部クランプは一切行わない (doccomment 遵守 / design.md §6.2)。
        /// </summary>
        private sealed class RecordingMotionApplier : IMotionApplier
        {
            public int ApplyCallCount { get; private set; }
            public MotionFrame LastFrame { get; private set; }
            public float LastWeight { get; private set; }
            public SlotSettings LastSettings { get; private set; }
            public List<float> ReceivedWeights { get; } = new List<float>();
            public int SetAvatarCallCount { get; private set; }
            public int DisposeCallCount { get; private set; }

            public void Apply(MotionFrame frame, float weight, SlotSettings settings)
            {
                ApplyCallCount++;
                LastFrame = frame;
                LastWeight = weight;  // クランプ処理をしない (契約)
                LastSettings = settings;
                ReceivedWeights.Add(weight);
            }

            public void SetAvatar(GameObject avatarRoot)
            {
                SetAvatarCallCount++;
            }

            public void Dispose()
            {
                DisposeCallCount++;
            }
        }
    }
}
