using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RealtimeAvatarController.Avatar.Builtin;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Builtin.Tests
{
    /// <summary>
    /// BuiltinAvatarProviderFactory の自己登録 / Registry 連携 EditMode 単体テスト
    /// (tasks.md T-6-4 / design.md §7・§11.1)。
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="BuiltinAvatarProviderFactory"/> の <c>RegisterRuntime()</c> 相当の登録処理を
    ///     Reflection 経由で呼び出した後に <see cref="IProviderRegistry.GetRegisteredTypeIds"/> に
    ///     "Builtin" が含まれること (T-6-4 RegisterRuntime_RegistersBuiltinTypeId)
    ///   - 同一 <c>typeId="Builtin"</c> の二重登録で <see cref="RegistryConflictException"/> が
    ///     スローされ、さらに <c>RegisterRuntime()</c> の catch ブロックで
    ///     <see cref="SlotErrorCategory.RegistryConflict"/> が <see cref="ISlotErrorChannel"/> へ
    ///     発行されること (T-6-4 Register_DuplicateTypeId_ThrowsRegistryConflictException)
    ///   - 登録後に <see cref="IProviderRegistry.Resolve"/> (有効な
    ///     <see cref="AvatarProviderDescriptor"/> を使用) を呼び出すと
    ///     <see cref="BuiltinAvatarProvider"/> が返ること
    ///     (T-6-4 Resolve_AfterRegistration_ReturnsBuiltinAvatarProvider)
    /// </para>
    ///
    /// <para>
    /// スタブ・モック戦略 (tasks.md T-6 前提):
    ///   - 各テスト開始・終了時に <see cref="RegistryLocator.ResetForTest"/> を呼び出し
    ///     Registry 汚染を防ぐ。EditMode では <c>[RuntimeInitializeOnLoadMethod]</c> の自動実行は
    ///     行われないため、Reflection で <c>RegisterRuntime()</c> を直接呼び出して登録をシミュレートする。
    ///   - <see cref="ISlotErrorChannel"/> モック <see cref="FakeSlotErrorChannel"/> を
    ///     <see cref="RegistryLocator.OverrideErrorChannel"/> で注入し、catch ブロックからの
    ///     <c>RegistryLocator.ErrorChannel.Publish(...)</c> 経由の発行内容を検証する。
    /// </para>
    ///
    /// Requirements: Req 1 AC 5, Req 1 AC 7, Req 5 AC 2, Req 9 AC 2
    /// </summary>
    [TestFixture]
    public class BuiltinAvatarProviderRegistrationTests
    {
        private FakeSlotErrorChannel _fakeChannel;
        private BuiltinAvatarProviderConfig _config;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _fakeChannel = new FakeSlotErrorChannel();
            RegistryLocator.OverrideErrorChannel(_fakeChannel);
            _config = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }
            RegistryLocator.ResetForTest();
        }

        // --- T-6-4 テストケース ---

        [Test]
        public void RegisterRuntime_RegistersBuiltinTypeId()
        {
            // [RuntimeInitializeOnLoadMethod] は EditMode では自動実行されないため
            // Reflection で private static RegisterRuntime() を直接呼び出す。
            InvokeRegisterRuntime();

            var typeIds = RegistryLocator.ProviderRegistry.GetRegisteredTypeIds();

            CollectionAssert.Contains(typeIds, BuiltinAvatarProviderFactory.BuiltinProviderTypeId,
                "RegisterRuntime() 呼び出し後は IProviderRegistry に typeId='Builtin' が登録されているべき。");
        }

        [Test]
        public void Register_DuplicateTypeId_ThrowsRegistryConflictException()
        {
            // (1) 直接 IProviderRegistry.Register() を二重呼び出しした場合に
            //     RegistryConflictException がスローされることを検証する
            //     (contracts.md §1.4 の競合ポリシー準拠)。
            var registry = RegistryLocator.ProviderRegistry;
            registry.Register(
                BuiltinAvatarProviderFactory.BuiltinProviderTypeId,
                new BuiltinAvatarProviderFactory());

            Assert.Throws<RegistryConflictException>(
                () => registry.Register(
                    BuiltinAvatarProviderFactory.BuiltinProviderTypeId,
                    new BuiltinAvatarProviderFactory()),
                "同一 typeId='Builtin' の二重登録は RegistryConflictException をスローするべき。");

            // (2) RegisterRuntime() は内部で try/catch を用いて RegistryConflictException を捕捉し、
            //     RegistryLocator.ErrorChannel へ SlotErrorCategory.RegistryConflict を発行する
            //     (design.md §7 自己登録エントリコード / §8 エラーパターン表)。
            //     Reflection 経由の二重呼び出しで発行経路を検証する。
            InvokeRegisterRuntime();

            Assert.IsTrue(_fakeChannel.HasReceived(SlotErrorCategory.RegistryConflict),
                "RegisterRuntime() の二重呼び出し時は ErrorChannel に SlotErrorCategory.RegistryConflict が発行されるべき。");
        }

        [Test]
        public void Resolve_AfterRegistration_ReturnsBuiltinAvatarProvider()
        {
            InvokeRegisterRuntime();

            var descriptor = new AvatarProviderDescriptor
            {
                ProviderTypeId = BuiltinAvatarProviderFactory.BuiltinProviderTypeId,
                Config = _config,
            };

            var provider = RegistryLocator.ProviderRegistry.Resolve(descriptor);

            Assert.IsInstanceOf<BuiltinAvatarProvider>(provider,
                "登録済み typeId='Builtin' の Descriptor で Resolve() すると BuiltinAvatarProvider が返るべき。");
        }

        // --- テストヘルパー ---

        /// <summary>
        /// <see cref="BuiltinAvatarProviderFactory"/> の private static <c>RegisterRuntime()</c> を
        /// Reflection で呼び出し、属性ベース自己登録エントリポイントの登録動作をシミュレートする。
        /// EditMode テストでは <c>[RuntimeInitializeOnLoadMethod]</c> が自動実行されないため、
        /// このヘルパーで登録タイミングを代替する。
        /// </summary>
        private static void InvokeRegisterRuntime()
        {
            var method = typeof(BuiltinAvatarProviderFactory).GetMethod(
                "RegisterRuntime",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null,
                "BuiltinAvatarProviderFactory.RegisterRuntime() が private static として存在するべき。");

            method.Invoke(null, null);
        }

        /// <summary>
        /// <see cref="ISlotErrorChannel"/> のテスト用スタブ。発行された <see cref="SlotError"/> を
        /// 順序付きリストに保持し、<see cref="HasReceived"/> で特定カテゴリの有無を検証する。
        /// UniRx 非依存とするため <see cref="Errors"/> は購読されない no-op 実装を返す。
        /// </summary>
        private sealed class FakeSlotErrorChannel : ISlotErrorChannel
        {
            private readonly List<SlotError> _received = new List<SlotError>();

            public IReadOnlyList<SlotError> Received => _received;

            public IObservable<SlotError> Errors => NoOpObservable.Instance;

            public void Publish(SlotError error)
            {
                _received.Add(error);
            }

            public bool HasReceived(SlotErrorCategory category)
            {
                foreach (var e in _received)
                {
                    if (e.Category == category) return true;
                }
                return false;
            }

            private sealed class NoOpObservable : IObservable<SlotError>
            {
                public static readonly NoOpObservable Instance = new NoOpObservable();
                public IDisposable Subscribe(IObserver<SlotError> observer) => NoOpDisposable.Instance;
            }

            private sealed class NoOpDisposable : IDisposable
            {
                public static readonly NoOpDisposable Instance = new NoOpDisposable();
                public void Dispose() { }
            }
        }
    }
}
