using NUnit.Framework;
using RealtimeAvatarController.Avatar.Builtin;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.Avatar.Builtin.Tests
{
    /// <summary>
    /// BuiltinAvatarProviderFactory の EditMode 単体テスト (tasks.md T-6-1 〜 T-6-3 / design.md §3.5)。
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="BuiltinAvatarProviderFactory.Create"/> がキャスト成功時に <see cref="BuiltinAvatarProvider"/> を返す
    ///     (T-6-1 キャスト成功テスト)
    /// </para>
    ///
    /// <para>
    /// スタブ・モック戦略 (tasks.md T-6 前提):
    ///   - 各テスト開始・終了時に <see cref="RegistryLocator.ResetForTest"/> を呼び出し
    ///     Registry 汚染を防ぐ。
    ///   - キャスト成功テストでは <see cref="ISlotErrorChannel"/> の発行は行われないため
    ///     デフォルトチャネルのまま検証する。
    /// </para>
    ///
    /// Requirements: Req 8 AC 2, Req 9 AC 2
    /// </summary>
    [TestFixture]
    public class BuiltinAvatarProviderFactoryTests
    {
        private BuiltinAvatarProviderConfig _config;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _config = ScriptableObject.CreateInstance<BuiltinAvatarProviderConfig>();
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
    }
}
