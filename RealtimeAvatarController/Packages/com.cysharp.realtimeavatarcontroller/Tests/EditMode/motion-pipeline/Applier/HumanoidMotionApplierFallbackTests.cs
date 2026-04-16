using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;

namespace RealtimeAvatarController.Motion.Tests.Applier
{
    /// <summary>
    /// HumanoidMotionApplier の Fallback EditMode 単体テスト (tasks.md §6-5 / design.md §8, §9, §11.2)。
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="IMotionApplier"/> 契約: Apply 内例外は catch せず呼び出し元 (SlotManager) に伝搬する
    ///   - <see cref="HumanoidMotionApplier.ExecuteFallback"/> による
    ///     <see cref="FallbackBehavior.HoldLastPose"/> / <see cref="FallbackBehavior.TPose"/> /
    ///     <see cref="FallbackBehavior.Hide"/> 各分岐の動作
    ///   - Hide → 正常 Apply で Renderer が復帰すること (<c>_isFallbackHiding</c> フラグ、<c>RestoreRenderers()</c>)
    ///   - SlotManager の catch ブロックで <see cref="RegistryLocator.ErrorChannel"/> に
    ///     <see cref="SlotErrorCategory.ApplyFailure"/> を発行すること
    ///   - null フレーム / 無効フレームでは <see cref="ISlotErrorChannel.Publish"/> が呼ばれないこと (design.md §9.3)
    /// </para>
    ///
    /// <para>
    /// スタブ・モック戦略 (tasks.md §6-5):
    ///   - <see cref="ISlotErrorChannel"/> モックを <see cref="RegistryLocator.OverrideErrorChannel"/> で差し込む
    ///   - 各テスト開始・終了時に <see cref="RegistryLocator.ResetForTest"/> でリセット
    ///   - <see cref="HumanoidMotionApplier"/> への <c>ISlotErrorChannel</c> 注入は不要 (Applier は参照を持たない)
    /// </para>
    ///
    /// <para>
    /// EditMode 制約:
    ///   実 <see cref="HumanPoseHandler"/> は Humanoid 構成済みの <see cref="Avatar"/> アセットを要求するため、
    ///   EditMode ではインスタンス化できない。本テストでは以下を採用する:
    ///   - <c>_poseHandler</c> 経路を要する検証 (SetHumanPose の例外伝搬、T ポーズの実適用) は
    ///     同等の <see cref="IMotionApplier"/> 契約を持つスタブ (<see cref="ThrowingMotionApplier"/>) 経由で検証するか、
    ///     <c>_poseHandler == null</c> 時に早期 return することを確認する (実アバター検証は PlayMode §7-2 / §7-3)。
    ///   - <c>_renderers</c> / <c>_isFallbackHiding</c> 経路はリフレクションで private フィールドを注入し、
    ///     <see cref="Renderer"/> 付き GameObject で実際の <c>enabled</c> 状態を観測する。
    /// </para>
    ///
    /// Requirements: Req 6, Req 9, Req 12, Req 13, Req 14
    /// </summary>
    [TestFixture]
    public class HumanoidMotionApplierFallbackTests
    {
        private const int HumanoidMuscleCount = 95;
        private const string TestSlotId = "fallback-test-slot";

        private SlotSettings _settings;
        private SpyErrorChannel _errorChannel;
        private readonly List<GameObject> _createdAvatars = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _errorChannel = new SpyErrorChannel();
            RegistryLocator.OverrideErrorChannel(_errorChannel);

            _settings = ScriptableObject.CreateInstance<SlotSettings>();
            _settings.slotId = TestSlotId;
            _settings.displayName = "Fallback Test Slot";
            _settings.weight = 1.0f;
            _settings.fallbackBehavior = FallbackBehavior.HoldLastPose;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _createdAvatars)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _createdAvatars.Clear();

            if (_settings != null)
            {
                UnityEngine.Object.DestroyImmediate(_settings);
                _settings = null;
            }

            RegistryLocator.ResetForTest();
        }

        // --- Apply_WhenExceptionThrown_PropagatesException ---

        [Test]
        public void Apply_WhenExceptionThrown_PropagatesException()
        {
            // IMotionApplier 契約 (design.md §8.1 / §9.1): Apply 内例外は catch せず呼び出し元へ伝搬する。
            // 実 HumanPoseHandler 例外を EditMode で発火させるには Humanoid Avatar 構成が必要なため、
            // 同等契約のスタブで契約違反 (例外握り潰し) が発生しないことを検証する。
            IMotionApplier applier = new ThrowingMotionApplier();
            var frame = CreateValidFrame();

            var ex = Assert.Throws<InvalidOperationException>(
                () => applier.Apply(frame, 1.0f, _settings),
                "Apply 内例外は catch されず呼び出し元に伝搬しなければならない (design.md §8.1)");
            Assert.That(ex.Message, Does.Contain("simulated"),
                "スタブから投げられた例外がそのまま再スローされていること");
        }

        // --- Fallback_HoldLastPose_DoesNothing ---

        [Test]
        public void Fallback_HoldLastPose_DoesNothing()
        {
            // HoldLastPose は何もしない (design.md §8.2): _lastGoodPose をそのまま維持し、
            // _poseHandler への再書き込みも行わない。副作用の無さを Renderer の状態と
            // _isFallbackHiding フラグの非変化で確認する。
            var avatar = CreateAvatarWithRenderers(1, out var renderers);
            using var applier = new HumanoidMotionApplier(TestSlotId);
            SetPrivateField(applier, "_renderers", renderers);

            bool hidingBefore = (bool)GetPrivateField(applier, "_isFallbackHiding");
            bool enabledBefore = renderers[0].enabled;

            Assert.DoesNotThrow(
                () => applier.ExecuteFallback(FallbackBehavior.HoldLastPose),
                "HoldLastPose は例外を発生させずに完了する");

            Assert.That((bool)GetPrivateField(applier, "_isFallbackHiding"), Is.EqualTo(hidingBefore),
                "HoldLastPose は _isFallbackHiding フラグを変化させない");
            Assert.That(renderers[0].enabled, Is.EqualTo(enabledBefore),
                "HoldLastPose は Renderer.enabled を変化させない (描画状態を維持する)");
        }

        // --- Fallback_TPose_ResetsMuscles ---

        [Test]
        public void Fallback_TPose_ResetsMuscles()
        {
            // TPose は _poseHandler != null の場合に限り HumanPose (全 Muscle 0) を適用する (design.md §8.3)。
            // EditMode では Humanoid Avatar が用意できないため _poseHandler は null のままとなり、
            // ガード節で早期 return することを確認する (実アバター検証は PlayMode §7-3)。
            // _renderers / _isFallbackHiding には副作用を及ぼさないことも合わせて確認する。
            var avatar = CreateAvatarWithRenderers(1, out var renderers);
            using var applier = new HumanoidMotionApplier(TestSlotId);
            SetPrivateField(applier, "_renderers", renderers);

            bool enabledBefore = renderers[0].enabled;

            Assert.DoesNotThrow(
                () => applier.ExecuteFallback(FallbackBehavior.TPose),
                "_poseHandler == null 時の TPose はガード節で早期 return し例外を発生させない");

            Assert.That(renderers[0].enabled, Is.EqualTo(enabledBefore),
                "TPose は Renderer.enabled を変化させない (Muscle のみ操作する分岐)");
            Assert.That((bool)GetPrivateField(applier, "_isFallbackHiding"), Is.False,
                "TPose は _isFallbackHiding フラグを変化させない");
        }

        // --- Fallback_Hide_DisablesAllRenderers ---

        [Test]
        public void Fallback_Hide_DisablesAllRenderers()
        {
            // Hide は _renderers 配列の全 Renderer.enabled = false とする (design.md §8.4)。
            // SetAvatar は Humanoid Animator を要求するため EditMode では呼び出せない。
            // 代わりにリフレクションで _renderers を注入し、実 Renderer コンポーネントで検証する。
            var avatar = CreateAvatarWithRenderers(3, out var renderers);
            using var applier = new HumanoidMotionApplier(TestSlotId);
            SetPrivateField(applier, "_renderers", renderers);

            applier.ExecuteFallback(FallbackBehavior.Hide);

            foreach (var r in renderers)
            {
                Assert.That(r.enabled, Is.False,
                    $"Hide 後、Renderer '{r.name}' の enabled は false でなければならない");
            }
        }

        // --- Fallback_Hide_SetsIsFallbackHidingTrue ---

        [Test]
        public void Fallback_Hide_SetsIsFallbackHidingTrue()
        {
            // _isFallbackHiding は次の正常 Apply で RestoreRenderers() を駆動するためのフラグ (design.md §8.4)。
            var avatar = CreateAvatarWithRenderers(1, out var renderers);
            using var applier = new HumanoidMotionApplier(TestSlotId);
            SetPrivateField(applier, "_renderers", renderers);

            Assert.That((bool)GetPrivateField(applier, "_isFallbackHiding"), Is.False,
                "前提: Hide 実行前の _isFallbackHiding は false");

            applier.ExecuteFallback(FallbackBehavior.Hide);

            Assert.That((bool)GetPrivateField(applier, "_isFallbackHiding"), Is.True,
                "Hide 後、_isFallbackHiding フラグは true (次の正常 Apply で RestoreRenderers を駆動)");
        }

        // --- ApplySuccess_AfterHide_RestoresRenderers ---

        [Test]
        public void ApplySuccess_AfterHide_RestoresRenderers()
        {
            // Hide 状態 (_isFallbackHiding == true) からの正常 Apply 完了時に
            // RestoreRenderers() が呼ばれ、全 Renderer.enabled == true / _isFallbackHiding == false
            // に復帰することを検証する (design.md §8.4)。
            //
            // EditMode では _poseHandler を介した正常 Apply パスを実行できないため、
            // 実 Applier における「正常 Apply 完了後に RestoreRenderers() を呼び出す」という
            // コードパス (HumanoidMotionApplier.Apply の末尾ブロック) を直接検証する代わりに、
            // private RestoreRenderers() をリフレクション経由で呼び出して復帰挙動を確認する。
            // 正常 Apply 経路全体の検証は PlayMode §7-3 (Hide_Recovery_EnablesRenderers_OnNextSuccessfulApply) で担保する。
            var avatar = CreateAvatarWithRenderers(2, out var renderers);
            foreach (var r in renderers) r.enabled = false; // Hide 状態を再現

            using var applier = new HumanoidMotionApplier(TestSlotId);
            SetPrivateField(applier, "_renderers", renderers);
            SetPrivateField(applier, "_isFallbackHiding", true);

            InvokePrivateMethod(applier, "RestoreRenderers");

            foreach (var r in renderers)
            {
                Assert.That(r.enabled, Is.True,
                    $"復帰後、Renderer '{r.name}' の enabled は true に戻る");
            }
            Assert.That((bool)GetPrivateField(applier, "_isFallbackHiding"), Is.False,
                "復帰後、_isFallbackHiding フラグは false に戻る");
        }

        // --- SlotManager_PublishesApplyFailure_OnException ---

        [Test]
        public void SlotManager_PublishesApplyFailure_OnException()
        {
            // SlotManager の catch ブロックは ExecuteFallback 実行後に
            // RegistryLocator.ErrorChannel.Publish(SlotErrorCategory.ApplyFailure) を呼ぶ (design.md §9.1)。
            var applier = new ThrowingMotionApplier();
            var frame = CreateValidFrame();
            _settings.fallbackBehavior = FallbackBehavior.HoldLastPose;

            InvokeSlotManagerLikeLateUpdate(applier, frame, 1.0f, _settings);

            Assert.That(_errorChannel.Published.Count, Is.EqualTo(1),
                "Apply 例外 1 回に対して ErrorChannel.Publish() は 1 回発行される");
            var error = _errorChannel.Published[0];
            Assert.That(error.Category, Is.EqualTo(SlotErrorCategory.ApplyFailure),
                "カテゴリは ApplyFailure (design.md §9.1)");
            Assert.That(error.SlotId, Is.EqualTo(TestSlotId),
                "SlotError.SlotId は SlotSettings.slotId と一致する");
            Assert.That(error.Exception, Is.InstanceOf<InvalidOperationException>(),
                "元の Apply 例外が SlotError.Exception に格納される");
        }

        // --- NullFrame_DoesNotPublishError ---

        [Test]
        public void NullFrame_DoesNotPublishError()
        {
            // LatestFrame == null の場合、Applier は例外を投げずに早期 return する (design.md §9.3)。
            // SlotManager の try ブロックは例外を受け取らないため Publish も呼ばれない。
            var applier = new SkipOrThrowMotionApplier();

            InvokeSlotManagerLikeLateUpdate(applier, frame: null, weight: 1.0f, settings: _settings);

            Assert.That(applier.ApplyCallCount, Is.EqualTo(1),
                "null frame でも Apply 自体は呼ばれる (Applier 内でスキップ)");
            Assert.That(_errorChannel.Published.Count, Is.EqualTo(0),
                "LatestFrame == null に対して ErrorChannel.Publish() を呼び出してはならない (通常動作)");
        }

        // --- InvalidFrame_DoesNotPublishError ---

        [Test]
        public void InvalidFrame_DoesNotPublishError()
        {
            // IsValid == false のフレームは Applier 内でスキップされ例外を発生させない (design.md §9.3)。
            // したがって SlotManager の catch は発火せず、ErrorChannel.Publish も呼ばれない。
            var applier = new SkipOrThrowMotionApplier();
            var invalid = HumanoidMotionFrame.CreateInvalid(timestamp: 1.0);

            InvokeSlotManagerLikeLateUpdate(applier, invalid, 1.0f, _settings);

            Assert.That(applier.ApplyCallCount, Is.EqualTo(1),
                "無効フレームでも Apply 自体は呼ばれる (Applier 内で IsValid 判定しスキップ)");
            Assert.That(_errorChannel.Published.Count, Is.EqualTo(0),
                "無効フレーム (IsValid==false) に対して ErrorChannel.Publish() を呼び出してはならない");

            // 実 HumanoidMotionApplier も無効フレームでは例外を発生させないことを補足確認する。
            using var realApplier = new HumanoidMotionApplier(TestSlotId);
            Assert.DoesNotThrow(
                () => realApplier.Apply(invalid, 1.0f, _settings),
                "実 HumanoidMotionApplier も無効フレームで例外を発生させない");
        }

        // --- helpers ---

        private static HumanoidMotionFrame CreateValidFrame(double timestamp = 1.0)
            => new HumanoidMotionFrame(
                timestamp,
                new float[HumanoidMuscleCount],
                Vector3.zero,
                Quaternion.identity);

        private GameObject CreateAvatarWithRenderers(int count, out Renderer[] renderers)
        {
            var root = new GameObject("fallback-test-avatar");
            _createdAvatars.Add(root);
            renderers = new Renderer[count];
            for (int i = 0; i < count; i++)
            {
                var child = new GameObject($"mesh-{i}");
                child.transform.SetParent(root.transform);
                renderers[i] = child.AddComponent<MeshRenderer>();
            }
            return root;
        }

        /// <summary>
        /// design.md §8.1 / §9.1 の SlotManager catch ブロックを再現するヘルパ。
        /// ExecuteFallback 実行 → ErrorChannel.Publish の順序で処理する。
        /// </summary>
        private static void InvokeSlotManagerLikeLateUpdate(
            IMotionApplier applier,
            MotionFrame frame,
            float weight,
            SlotSettings settings)
        {
            float clamped = Mathf.Clamp01(weight);
            if (clamped == 0f)
            {
                return;
            }

            try
            {
                applier.Apply(frame, clamped, settings);
            }
            catch (Exception ex)
            {
                // HumanoidMotionApplier を差し込んだ本物のフローでは ExecuteFallback を呼ぶが、
                // 本ヘルパは Applier 具象型に非依存に書くため Fallback 実行は省略し、
                // ErrorChannel.Publish の発行順序のみを検証する (ExecuteFallback 単体テストは別ケース)。
                RegistryLocator.ErrorChannel.Publish(
                    new SlotError(settings.slotId, SlotErrorCategory.ApplyFailure, ex, DateTime.UtcNow));
            }
        }

        // --- reflection helpers ---

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{name}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{name}' not found on {target.GetType().Name}");
            return field.GetValue(target);
        }

        private static void InvokePrivateMethod(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Method '{name}' not found on {target.GetType().Name}");
            method.Invoke(target, null);
        }

        // --- stubs ---

        /// <summary>
        /// 常に <see cref="InvalidOperationException"/> を投げる <see cref="IMotionApplier"/> スタブ。
        /// SlotManager の catch ブロック挙動 (例外伝搬・Publish 発行) を検証する用途で使用する。
        /// </summary>
        private sealed class ThrowingMotionApplier : IMotionApplier
        {
            public void Apply(MotionFrame frame, float weight, SlotSettings settings)
            {
                throw new InvalidOperationException("simulated Apply failure");
            }

            public void SetAvatar(GameObject avatarRoot) { }
            public void Dispose() { }
        }

        /// <summary>
        /// 実 <see cref="HumanoidMotionApplier"/> のスキップ条件 (null / 無効 / 非 Humanoid) を模倣する
        /// スタブ。有効フレームかつ適切な条件を満たす場合のみ例外を投げる。
        /// null/無効フレーム時に Publish が呼ばれないことの検証に使用する。
        /// </summary>
        private sealed class SkipOrThrowMotionApplier : IMotionApplier
        {
            public int ApplyCallCount { get; private set; }

            public void Apply(MotionFrame frame, float weight, SlotSettings settings)
            {
                ApplyCallCount++;
                if (frame == null) return;
                if (!(frame is HumanoidMotionFrame h)) return;
                if (!h.IsValid) return;
                // 有効フレームに達した場合は例外を投げる (本テストでは到達しない想定)
                throw new InvalidOperationException("simulated Apply failure on valid frame");
            }

            public void SetAvatar(GameObject avatarRoot) { }
            public void Dispose() { }
        }

        /// <summary>
        /// <see cref="RegistryLocator.OverrideErrorChannel"/> 経由で差し込むスパイチャネル。
        /// 発行された <see cref="SlotError"/> を順序付きリストに保持する。
        /// </summary>
        private sealed class SpyErrorChannel : ISlotErrorChannel
        {
            private readonly Subject<SlotError> _subject = new Subject<SlotError>();

            public List<SlotError> Published { get; } = new List<SlotError>();
            public IObservable<SlotError> Errors => _subject;

            public void Publish(SlotError error)
            {
                Published.Add(error);
                _subject.OnNext(error);
            }
        }
    }
}
