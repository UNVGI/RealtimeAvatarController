using System;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// VMCMoCapSourceFactory.Create() のキャスト検証 EditMode テスト
    /// (tasks.md タスク 5.1 / design.md §4.5 /
    /// requirements.md 要件 5.2, 5.4, 5.5)。
    ///
    /// <para>
    /// 検証対象:
    ///   - VMCMoCapSourceConfig を MoCapSourceConfigBase として Create() に渡した場合、
    ///     VMCMoCapSource が正常に生成される (要件 5.5 / typeId "VMC" の新 Adapter 生成)
    ///   - 別の MoCapSourceConfigBase 派生型を渡した場合、ArgumentException がスローされ、
    ///     受け取った型名が例外メッセージに含まれる (要件 5.4)
    ///   - null を渡した場合、ArgumentException がスローされる (要件 5.4)
    ///   - ScriptableObject.CreateInstance&lt;VMCMoCapSourceConfig&gt;() で動的生成した Config を
    ///     渡した場合、VMCMoCapSource が正常に生成される (要件 5.2 / シナリオ Y)
    /// </para>
    ///
    /// <para>
    /// テスト独立性: <c>RegistryLocator.ResetForTest()</c> を <c>[SetUp]</c> / <c>[TearDown]</c>
    /// の両方で呼び出し、<see cref="VMCMoCapSourceFactory"/> の
    /// <c>[RuntimeInitializeOnLoadMethod]</c> 自己登録による他テストへの副作用を排除する
    /// (tasks.md タスク 10-2)。
    /// </para>
    /// </summary>
    [TestFixture]
    public class VmcConfigCastTests
    {
        private const string VmcSourceTypeId = "VMC";

        private VMCMoCapSourceConfig _config;
        private OtherMoCapSourceConfig _otherConfig;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }
            if (_otherConfig != null)
            {
                UnityEngine.Object.DestroyImmediate(_otherConfig);
                _otherConfig = null;
            }
            RegistryLocator.ResetForTest();
        }

        [Test]
        public void Create_WithVMCMoCapSourceConfigAsBase_ReturnsVMCMoCapSource()
        {
            var factory = new VMCMoCapSourceFactory();
            MoCapSourceConfigBase config = _config;

            var source = factory.Create(config);

            Assert.IsNotNull(source, "VMCMoCapSource は null でないインスタンスが返されるべき。");
            Assert.IsInstanceOf<VMCMoCapSource>(source,
                "VMCMoCapSourceConfig を受け取った Factory は VMCMoCapSource を返すべき (tasks.md 5.2 / 要件 5.5)。");
            Assert.That(source.SourceType, Is.EqualTo(VmcSourceTypeId),
                "Config cast 経路で生成した source は typeId='VMC' を維持するべき。");
        }

        [Test]
        public void Create_WithOtherConfigType_ThrowsArgumentException_WithTypeNameInMessage()
        {
            var factory = new VMCMoCapSourceFactory();
            _otherConfig = ScriptableObject.CreateInstance<OtherMoCapSourceConfig>();

            var ex = Assert.Throws<ArgumentException>(() => factory.Create(_otherConfig));

            StringAssert.Contains(nameof(OtherMoCapSourceConfig), ex.Message,
                "型不一致時の ArgumentException メッセージには受け取った型名が含まれるべき (要件 5.4)。");
        }

        [Test]
        public void Create_WithNullConfig_ThrowsArgumentException()
        {
            var factory = new VMCMoCapSourceFactory();

            Assert.Throws<ArgumentException>(() => factory.Create(null),
                "null を渡された場合は ArgumentException をスローすべき (要件 5.4)。");
        }

        [Test]
        public void Create_WithDynamicallyCreatedConfig_ReturnsVMCMoCapSource()
        {
            // シナリオ Y (要件 5.2): ScriptableObject.CreateInstance で動的生成した Config を
            // SO アセット経由のインスタンスと同一コードパスで扱えること。
            var dynamicConfig = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            try
            {
                var factory = new VMCMoCapSourceFactory();

                var source = factory.Create(dynamicConfig);

                Assert.IsNotNull(source, "動的生成 Config でも VMCMoCapSource が返されるべき。");
                Assert.IsInstanceOf<VMCMoCapSource>(source,
                    "動的生成 Config は SO アセットと同一コードパスで VMCMoCapSource を生成すべき (要件 5.2 / 5.5)。");
                Assert.That(source.SourceType, Is.EqualTo(VmcSourceTypeId),
                    "動的生成 Config 経路でも source typeId='VMC' を維持するべき。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dynamicConfig);
            }
        }

        /// <summary>
        /// キャスト失敗経路検証用のダミー MoCapSourceConfigBase 派生型。
        /// VMCMoCapSourceFactory が VMCMoCapSourceConfig 以外を拒否することを確認するため使用する。
        /// </summary>
        private sealed class OtherMoCapSourceConfig : MoCapSourceConfigBase
        {
        }
    }
}
