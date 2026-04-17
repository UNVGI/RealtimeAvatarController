using System;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// VMCMoCapSourceFactory.Create() のキャスト検証 EditMode テスト
    /// (tasks.md タスク 2-1 / design.md §5.3, §10.1 / requirements.md 要件 9-3, 9-4, 9-11, 9-12, 10-2)。
    ///
    /// <para>
    /// TDD 先行作成: 本テストファイル作成時点では以下の型は未実装である。
    ///   - <c>RealtimeAvatarController.MoCap.VMC.VMCMoCapSourceConfig</c>
    ///   - <c>RealtimeAvatarController.MoCap.VMC.VMCMoCapSourceFactory</c>
    ///   - <c>RealtimeAvatarController.MoCap.VMC.VmcMoCapSource</c>
    /// したがって本ファイルはタスク 2-2・タスク 8-2 の実装完了までコンパイルエラーとなってよい
    /// (tasks.md タスク 2-1 注記および tasks.md タスク 10-2 で最終完成)。
    /// </para>
    ///
    /// <para>
    /// 検証対象:
    ///   - VMCMoCapSourceConfig を MoCapSourceConfigBase として Create() に渡した場合、
    ///     VmcMoCapSource が正常に生成される (要件 9-3, 9-12 / シナリオ X の等価性)
    ///   - 別の MoCapSourceConfigBase 派生型を渡した場合、ArgumentException がスローされ、
    ///     受け取った型名が例外メッセージに含まれる (要件 9-4)
    ///   - null を渡した場合、ArgumentException がスローされる (要件 9-4)
    ///   - ScriptableObject.CreateInstance&lt;VMCMoCapSourceConfig&gt;() で動的生成した Config を
    ///     渡した場合、VmcMoCapSource が正常に生成される (要件 9-11, 9-12 / シナリオ Y)
    /// </para>
    /// </summary>
    [TestFixture]
    public class VmcConfigCastTests
    {
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
        public void Create_WithVMCMoCapSourceConfigAsBase_ReturnsVmcMoCapSource()
        {
            var factory = new VMCMoCapSourceFactory();
            MoCapSourceConfigBase config = _config;

            var source = factory.Create(config);

            Assert.IsNotNull(source, "VmcMoCapSource は null でないインスタンスが返されるべき。");
            Assert.IsInstanceOf<VmcMoCapSource>(source,
                "VMCMoCapSourceConfig を受け取った Factory は VmcMoCapSource を返すべき。");
        }

        [Test]
        public void Create_WithOtherConfigType_ThrowsArgumentException_WithTypeNameInMessage()
        {
            var factory = new VMCMoCapSourceFactory();
            _otherConfig = ScriptableObject.CreateInstance<OtherMoCapSourceConfig>();

            var ex = Assert.Throws<ArgumentException>(() => factory.Create(_otherConfig));

            StringAssert.Contains(nameof(OtherMoCapSourceConfig), ex.Message,
                "型不一致時の ArgumentException メッセージには受け取った型名が含まれるべき (要件 9-4)。");
        }

        [Test]
        public void Create_WithNullConfig_ThrowsArgumentException()
        {
            var factory = new VMCMoCapSourceFactory();

            Assert.Throws<ArgumentException>(() => factory.Create(null),
                "null を渡された場合は ArgumentException をスローすべき (要件 9-4)。");
        }

        [Test]
        public void Create_WithDynamicallyCreatedConfig_ReturnsVmcMoCapSource()
        {
            // シナリオ Y (要件 9-11, 9-12): ScriptableObject.CreateInstance で動的生成した Config を
            // SO アセット経由のインスタンスと同一コードパスで扱えること。
            var dynamicConfig = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            try
            {
                var factory = new VMCMoCapSourceFactory();

                var source = factory.Create(dynamicConfig);

                Assert.IsNotNull(source, "動的生成 Config でも VmcMoCapSource が返されるべき。");
                Assert.IsInstanceOf<VmcMoCapSource>(source,
                    "動的生成 Config は SO アセットと同一コードパスで VmcMoCapSource を生成すべき (要件 9-12)。");
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
