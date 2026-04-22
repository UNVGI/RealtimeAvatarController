using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Avatar.Builtin;
using RealtimeAvatarController.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealtimeAvatarController.Avatar.Builtin.Tests
{
    /// <summary>
    /// BuiltinAvatarProviderFactory の EditMode 単体テスト (tasks.md T-6-1 〜 T-6-3 / design.md §3.5)。
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="BuiltinAvatarProviderFactory.Create"/> がキャスト成功時に <see cref="BuiltinAvatarProvider"/> を返す
    ///     (T-6-1 キャスト成功テスト)
    ///   - キャスト失敗時に <see cref="ArgumentException"/> をスローし
    ///     <see cref="ISlotErrorChannel"/> へ <see cref="SlotErrorCategory.InitFailure"/> を発行する
    ///     (T-6-2 キャスト失敗・ErrorChannel 発行テスト / validation-design.md Minor #1)
    ///   - 同一 Factory インスタンスに対する複数回の <see cref="BuiltinAvatarProviderFactory.Create"/>
    ///     呼び出しが互いに干渉せず、独立した <see cref="BuiltinAvatarProvider"/> インスタンスを返す
    ///     (T-6-3 ステートレス設計テスト)
    /// </para>
    ///
    /// <para>
    /// スタブ・モック戦略 (tasks.md T-6 前提):
    ///   - 各テスト開始・終了時に <see cref="RegistryLocator.ResetForTest"/> を呼び出し
    ///     Registry 汚染を防ぐ。
    ///   - <see cref="ISlotErrorChannel"/> モック <see cref="FakeSlotErrorChannel"/> を
    ///     <see cref="BuiltinAvatarProviderFactory"/> のコンストラクタに注入して発行内容を検証する。
    /// </para>
    ///
    /// Requirements: Req 8 AC 2, Req 8 AC 3, Req 8 AC 5, Req 9 AC 2
    /// </summary>
    [TestFixture]
    public class BuiltinAvatarProviderFactoryTests
    {
        private BuiltinAvatarProviderConfig _config;
        private FakeSlotErrorChannel _fakeChannel;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _config = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
            _fakeChannel = new FakeSlotErrorChannel();
            RegistryLocator.OverrideErrorChannel(_fakeChannel);
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
                _config = null;
            }
            RegistryLocator.ResetForTest();
        }

        [Test]
        public void Create_WithValidConfig_ReturnsBuiltinAvatarProvider()
        {
            var factory = new BuiltinAvatarProviderFactory();

            var provider = factory.Create(_config);

            Assert.IsInstanceOf<BuiltinAvatarProvider>(provider);
        }

        [Test]
        public void Create_WithInvalidConfig_ThrowsArgumentException()
        {
            var factory = new BuiltinAvatarProviderFactory();
            var invalidConfig = ScriptableObject.CreateInstance<NonBuiltinProviderConfig>();
            try
            {
                Assert.Throws<ArgumentException>(() => factory.Create(invalidConfig));
            }
            finally
            {
                Object.DestroyImmediate(invalidConfig);
            }
        }

        [Test]
        public void Create_WithInvalidConfig_PublishesInitFailureToErrorChannel()
        {
            // (1) channel あり → InitFailure が FakeSlotErrorChannel に発行される。
            var factory = new BuiltinAvatarProviderFactory(_fakeChannel);
            var invalidConfig = ScriptableObject.CreateInstance<NonBuiltinProviderConfig>();
            try
            {
                Assert.Throws<ArgumentException>(() => factory.Create(invalidConfig));
                Assert.IsTrue(_fakeChannel.HasReceived(SlotErrorCategory.InitFailure),
                    "キャスト失敗時に SlotErrorCategory.InitFailure が発行されるべき。");
            }
            finally
            {
                Object.DestroyImmediate(invalidConfig);
            }

            // (2) validation-design.md Minor #1: _errorChannel が null でも ArgumentException は
            //     スローされる (channel?.Publish の null 条件演算子による null 安全性)。
            //     Factory の _errorChannel が null の場合は RegistryLocator.ErrorChannel へフォールバックするが、
            //     いずれのケースでもキャスト失敗時の ArgumentException は必ずスローされることを確認する。
            var nullChannelFactory = new BuiltinAvatarProviderFactory(null);
            var invalidConfig2 = ScriptableObject.CreateInstance<NonBuiltinProviderConfig>();
            try
            {
                Assert.Throws<ArgumentException>(() => nullChannelFactory.Create(invalidConfig2));
            }
            finally
            {
                Object.DestroyImmediate(invalidConfig2);
            }
        }

        [Test]
        public void Create_IsStateless_MultipleCalls_DoNotInterfere()
        {
            // tasks.md T-6-3 / design.md §3.5 ステートレス設計:
            //   Factory は _errorChannel のみを読み取り専用参照として保持し、
            //   Create() 呼び出し間で状態を共有しない。複数回の呼び出しはそれぞれ
            //   独立した BuiltinAvatarProvider インスタンスを生成し、参照が異なることを確認する。
            var factory = new BuiltinAvatarProviderFactory(_fakeChannel);

            var provider1 = factory.Create(_config);
            var provider2 = factory.Create(_config);

            Assert.IsInstanceOf<BuiltinAvatarProvider>(provider1);
            Assert.IsInstanceOf<BuiltinAvatarProvider>(provider2);
            Assert.AreNotSame(provider1, provider2,
                "同一 Factory に対する複数回の Create() 呼び出しは互いに独立した Provider インスタンスを返すべき。");
        }

        // --- テストヘルパー ---

        /// <summary>
        /// <see cref="ProviderConfigBase"/> を継承するだけの非 Builtin 型。
        /// <see cref="BuiltinAvatarProviderFactory.Create"/> のキャスト失敗経路を検証するため使用する。
        /// </summary>
        private sealed class NonBuiltinProviderConfig : ProviderConfigBase
        {
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
