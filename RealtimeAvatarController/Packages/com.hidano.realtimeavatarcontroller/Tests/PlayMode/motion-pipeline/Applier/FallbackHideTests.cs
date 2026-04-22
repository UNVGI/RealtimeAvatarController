using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;
using RealtimeAvatarController.Motion.Tests.PlayMode.Fixtures;

namespace RealtimeAvatarController.Motion.Tests.PlayMode.Applier
{
    /// <summary>
    /// <see cref="FallbackBehavior.Hide"/> の視覚動作確認テスト (tasks.md §7-3 / design.md §8.4)。
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="FallbackBehavior.Hide"/> が設定された Slot で Apply 例外が発生した場合、
    ///     アバターの全 <see cref="Renderer.enabled"/> が <c>false</c> になること (実 Renderer コンポーネントで確認)
    ///   - Hide 後も GameObject が <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> されておらず
    ///     <c>activeInHierarchy == true</c> を維持していること
    ///   - Hide 状態から次フレームの正常 <see cref="HumanoidMotionApplier.Apply"/> 完了後に
    ///     全 <see cref="Renderer.enabled"/> が <c>true</c> に復帰していること
    /// </para>
    ///
    /// <para>
    /// EditMode (tasks.md §6-5) ではリフレクションでモック <see cref="Renderer"/> 配列を
    /// 注入して検証するが、本 PlayMode テストでは <see cref="TestHumanoidAvatarBuilder"/>
    /// が生成する実 Humanoid アバターに付属する実 <see cref="Renderer"/> コンポーネントに対して
    /// Hide / 復帰の経路を検証する (design.md §13.2)。
    /// </para>
    ///
    /// <para>
    /// SlotManager の catch ブロック自体 (ExecuteFallback の起点となる例外伝搬経路) の
    /// 検証は tasks.md §6-5 EditMode テストで担保する。本テストは SlotManager が
    /// <see cref="HumanoidMotionApplier.ExecuteFallback"/> を呼び出した後の
    /// 実 Renderer コンポーネントの状態変化を検証する。
    /// </para>
    ///
    /// Requirements: Req 9, Req 12, Req 14
    /// </summary>
    [TestFixture]
    public class FallbackHideTests
    {
        private const string TestSlotId = "fallback-hide-test-slot";

        private SlotSettings _settings;
        private readonly List<GameObject> _createdAvatars = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SlotSettings>();
            _settings.slotId = TestSlotId;
            _settings.displayName = "Fallback Hide Test Slot";
            _settings.weight = 1.0f;
            _settings.fallbackBehavior = FallbackBehavior.Hide;
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
        }

        // --- Hide_DisablesAllRenderers_OnApplyFailure ---

        [Test]
        public void Hide_DisablesAllRenderers_OnApplyFailure()
        {
            // design.md §9.1: SlotManager は Apply 例外を catch し、settings.fallbackBehavior に
            // 従って ExecuteFallback() を呼び出す。本テストは FallbackBehavior.Hide が設定された
            // Slot で SlotManager が ExecuteFallback(Hide) を起動した際、アバターに付属する
            // 実 Renderer コンポーネントの enabled が false になることを検証する。
            var avatar = BuildAvatar("hide-disables-avatar");
            var renderers = avatar.GetComponentsInChildren<Renderer>(includeInactive: true);
            Assert.That(renderers.Length, Is.GreaterThan(0), "前提: テストアバターは Renderer を含む");
            foreach (var r in renderers)
            {
                Assert.That(r.enabled, Is.True, $"前提: Renderer '{r.name}' は初期状態で enabled == true");
            }

            using var applier = new HumanoidMotionApplier(TestSlotId);
            applier.SetAvatar(avatar);

            // Settings に Hide が設定された状態で SlotManager の catch 後フローを再現する。
            Assert.That(_settings.fallbackBehavior, Is.EqualTo(FallbackBehavior.Hide),
                "前提: SlotSettings.fallbackBehavior == Hide");
            applier.ExecuteFallback(_settings.fallbackBehavior);

            foreach (var r in renderers)
            {
                Assert.That(r.enabled, Is.False,
                    $"Hide 後、実 Renderer '{r.name}' の enabled は false である (design.md §8.4)");
            }
        }

        // --- Hide_KeepsGameObjectAlive ---

        [Test]
        public void Hide_KeepsGameObjectAlive()
        {
            // design.md §8.4 / §11.2 注記: Hide の実装は Renderer.enabled = false であり、
            // GameObject.SetActive(false) ではない。したがって GameObject / Transform 階層は
            // そのまま生存し、Slot ライフサイクルや他コンポーネントへの副作用は生じない
            // (requirements Req 12 AC4 準拠)。
            var avatar = BuildAvatar("hide-alive-avatar");
            using var applier = new HumanoidMotionApplier(TestSlotId);
            applier.SetAvatar(avatar);

            applier.ExecuteFallback(FallbackBehavior.Hide);

            Assert.That(avatar, Is.Not.Null,
                "Hide 後も GameObject は Destroy されておらず参照が生存している");
            Assert.That(avatar.activeSelf, Is.True,
                "Hide は GameObject.SetActive(false) を呼ばない (activeSelf は true を維持)");
            Assert.That(avatar.activeInHierarchy, Is.True,
                "Hide 後も GameObject は階層アクティブのまま (Renderer.enabled のみを操作する)");
            Assert.That(avatar.transform.childCount, Is.GreaterThan(0),
                "Hide は子 Transform 階層を破壊しない");
        }

        // --- Hide_Recovery_EnablesRenderers_OnNextSuccessfulApply ---

        [Test]
        public void Hide_Recovery_EnablesRenderers_OnNextSuccessfulApply()
        {
            // design.md §8.4 Hide からの復帰: 次フレームの Apply() が例外なく正常完了した場合、
            // HumanoidMotionApplier は内部で RestoreRenderers() を呼び出し、全 Renderer.enabled を
            // true に戻す。本テストは実 Humanoid アバター + 実 HumanPoseHandler 経路で
            // Hide → 正常 Apply → Renderer 復帰のフル経路を検証する (EditMode では Humanoid Avatar を
            // 構築できないため本 PlayMode テストで担保)。
            var avatar = BuildAvatar("hide-recovery-avatar");
            var renderers = avatar.GetComponentsInChildren<Renderer>(includeInactive: true);
            Assert.That(renderers.Length, Is.GreaterThan(0), "前提: テストアバターは Renderer を含む");

            using var applier = new HumanoidMotionApplier(TestSlotId);
            applier.SetAvatar(avatar);

            applier.ExecuteFallback(FallbackBehavior.Hide);
            foreach (var r in renderers)
            {
                Assert.That(r.enabled, Is.False,
                    $"前提: Hide 実行後 Renderer '{r.name}' は enabled == false");
            }

            var validFrame = CreateValidFrame();
            Assert.DoesNotThrow(
                () => applier.Apply(validFrame, 1.0f, _settings),
                "Hide 状態から正常な HumanoidMotionFrame を Apply すると例外なく完了する");

            foreach (var r in renderers)
            {
                Assert.That(r.enabled, Is.True,
                    $"正常 Apply 完了後、Renderer '{r.name}' の enabled は true に復帰する (design.md §8.4)");
            }
        }

        // --- helpers ---

        private GameObject BuildAvatar(string name)
        {
            var go = TestHumanoidAvatarBuilder.Build(name);
            _createdAvatars.Add(go);
            return go;
        }

        private static HumanoidMotionFrame CreateValidFrame()
        {
            var muscles = new float[HumanTrait.MuscleCount];
            for (int i = 0; i < muscles.Length; i++)
            {
                muscles[i] = 0.3f;
            }
            return new HumanoidMotionFrame(
                timestamp: 2.0,
                muscles: muscles,
                rootPosition: new Vector3(0f, 1.0f, 0f),
                rootRotation: Quaternion.identity);
        }
    }
}
