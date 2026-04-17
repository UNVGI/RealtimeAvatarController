using System;
using System.Reflection;
using NUnit.Framework;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// VMCMoCapSourceFactory の属性ベース自己登録 EditMode 単体テスト
    /// (tasks.md タスク 8-1 / design.md §7・§10.1 / requirements.md 要件 9-5, 9-7, 9-8, 9-9, 9-10, 10-2, 10-5)。
    ///
    /// <para>
    /// TDD 先行作成: 本テストファイル作成時点では以下の型は未実装である。
    ///   - <c>RealtimeAvatarController.MoCap.VMC.VMCMoCapSourceFactory</c>
    /// したがって本ファイルはタスク 8-2 の実装完了までコンパイルエラーとなってよい
    /// (tasks.md タスク 8-1 注記および tasks.md タスク 10-4 で最終完成)。
    /// </para>
    ///
    /// <para>
    /// 検証対象:
    ///   - <see cref="VMCMoCapSourceFactory"/> の private static <c>RegisterRuntime()</c> を
    ///     Reflection 経由で呼び出した後、<see cref="IMoCapSourceRegistry.GetRegisteredTypeIds"/>
    ///     に typeId="VMC" が含まれること (要件 9-5, 9-7)
    ///   - 同一 typeId="VMC" の二重登録 (直接 <see cref="IMoCapSourceRegistry.Register"/> 呼び出し) は
    ///     <see cref="RegistryConflictException"/> をスローすること (要件 9-9 / contracts.md §1.4)
    ///   - <see cref="RegistryLocator.ResetForTest"/> 後に再度 <c>RegisterRuntime()</c> を呼び出すと、
    ///     競合なく "VMC" が再登録できること (要件 9-8 / Domain Reload OFF 下の二重登録防止)
    ///   - <c>[TearDown]</c> で <see cref="RegistryLocator.ResetForTest"/> を呼び出すことにより
    ///     他テストへの登録状態汚染が発生しないこと (要件 10-5 / テスト独立性)
    /// </para>
    ///
    /// <para>
    /// スタブ・モック戦略 (tasks.md タスク 8-1 / 10-4 前提):
    ///   - EditMode では <c>[RuntimeInitializeOnLoadMethod]</c> が自動実行されないため、
    ///     Reflection で private static <c>RegisterRuntime()</c> を直接呼び出して登録タイミングを代替する
    ///     (<see cref="InvokeRegisterRuntime"/>)。
    ///   - 各テスト開始・終了時に <see cref="RegistryLocator.ResetForTest"/> を呼び出し、
    ///     静的 <see cref="RegistryLocator.MoCapSourceRegistry"/> の登録状態汚染を防ぐ
    ///     (要件 10-5)。
    /// </para>
    /// </summary>
    [TestFixture]
    public class VmcFactoryRegistrationTests
    {
        /// <summary>
        /// VMCMoCapSourceFactory が登録する typeId (タスク 8-2 / design.md §10.1)。
        /// 実装完了時に <c>VMCMoCapSourceFactory.VmcSourceTypeId</c> 相当の public const が
        /// 定義される予定だが、本テストは文字列リテラル "VMC" での一致をゴールデン値として扱う
        /// (要件 9-5 / requirements.md の typeId 規定)。
        /// </summary>
        private const string VmcSourceTypeId = "VMC";

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            RegistryLocator.ResetForTest();
        }

        // --- タスク 8-1 テストケース ---

        /// <summary>
        /// ケース 1: <c>[SetUp]</c> で <see cref="RegistryLocator.ResetForTest"/> 実行後、
        /// Reflection で <c>RegisterRuntime()</c> を呼び出すと
        /// <see cref="IMoCapSourceRegistry.GetRegisteredTypeIds"/> に "VMC" が含まれる
        /// (要件 9-5, 9-7)。
        /// </summary>
        [Test]
        public void RegisterRuntime_AddsVmcTypeId_ToMoCapSourceRegistry()
        {
            InvokeRegisterRuntime();

            var typeIds = RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds();

            CollectionAssert.Contains(typeIds, VmcSourceTypeId,
                "RegisterRuntime() 呼び出し後は IMoCapSourceRegistry に typeId='VMC' が登録されているべき (要件 9-5, 9-7)。");
        }

        /// <summary>
        /// ケース 2: 同一 typeId="VMC" を直接 <see cref="IMoCapSourceRegistry.Register"/> で
        /// 二重登録した場合、<see cref="RegistryConflictException"/> がスローされる
        /// (要件 9-9 / contracts.md §1.4 競合ポリシー)。
        /// </summary>
        [Test]
        public void Register_DuplicateVmcTypeId_ThrowsRegistryConflictException()
        {
            var registry = RegistryLocator.MoCapSourceRegistry;

            registry.Register(VmcSourceTypeId, new VMCMoCapSourceFactory());

            Assert.Throws<RegistryConflictException>(
                () => registry.Register(VmcSourceTypeId, new VMCMoCapSourceFactory()),
                "同一 typeId='VMC' の二重登録は RegistryConflictException をスローするべき (要件 9-9)。");
        }

        /// <summary>
        /// ケース 3: <see cref="RegistryLocator.ResetForTest"/> 後に <c>RegisterRuntime()</c> を
        /// 再度呼び出しても、競合なく "VMC" が再登録できる (要件 9-8 / Domain Reload OFF 下で
        /// SubsystemRegistration → BeforeSceneLoad の順序保証により二重登録が起きない)。
        /// </summary>
        [Test]
        public void ResetForTest_ThenRegisterRuntime_ReregistersVmcTypeIdSuccessfully()
        {
            // (1) 初回登録
            InvokeRegisterRuntime();
            CollectionAssert.Contains(
                RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds(),
                VmcSourceTypeId,
                "前提: 初回 RegisterRuntime() で 'VMC' が登録されていること。");

            // (2) ResetForTest で Registry インスタンスを破棄 (Domain Reload OFF 起動時相当)
            RegistryLocator.ResetForTest();

            var registryAfterReset = RegistryLocator.MoCapSourceRegistry;
            CollectionAssert.DoesNotContain(
                registryAfterReset.GetRegisteredTypeIds(),
                VmcSourceTypeId,
                "ResetForTest 後は Registry が新インスタンスとなり 'VMC' は残っていないべき (要件 9-8)。");

            // (3) 再登録が競合なく成功すること
            Assert.DoesNotThrow(() => InvokeRegisterRuntime(),
                "ResetForTest 後の再 RegisterRuntime() は競合なく成功するべき (要件 9-8)。");

            CollectionAssert.Contains(
                RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds(),
                VmcSourceTypeId,
                "再登録後の IMoCapSourceRegistry に typeId='VMC' が含まれているべき。");
        }

        /// <summary>
        /// ケース 4: <c>[TearDown]</c> の <see cref="RegistryLocator.ResetForTest"/> により、
        /// 前テストの登録状態が次テスト開始時に残っていない (要件 10-5 / テスト独立性)。
        /// 本テストは <c>[SetUp]</c> 直後に "VMC" が未登録であることを確認し、
        /// もし他テストが TearDown を欠いていれば失敗するように設計されている。
        /// </summary>
        [Test]
        public void TearDown_ResetForTest_LeavesRegistryCleanForNextTest()
        {
            var typeIdsAtStart = RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds();

            CollectionAssert.DoesNotContain(typeIdsAtStart, VmcSourceTypeId,
                "[SetUp] 時点で 'VMC' が未登録であるべき (他テストの TearDown が副作用を残していないこと / 要件 10-5)。");
        }

        // --- テストヘルパー ---

        /// <summary>
        /// <see cref="VMCMoCapSourceFactory"/> の private static <c>RegisterRuntime()</c> を
        /// Reflection で呼び出し、<c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c>
        /// の自動実行をシミュレートする。
        /// EditMode テストでは Unity による属性自動実行が行われないため、本ヘルパーで代替する
        /// (tasks.md タスク 8-1 手順 / 設計: design.md §7 自己登録エントリコード)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="TargetInvocationException"/> が発生した場合は内包する実例外を再スローして
        /// <see cref="Assert.Throws"/> 等で直接捕捉できるようにする。
        /// </para>
        /// </remarks>
        private static void InvokeRegisterRuntime()
        {
            var method = typeof(VMCMoCapSourceFactory).GetMethod(
                "RegisterRuntime",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null,
                "VMCMoCapSourceFactory.RegisterRuntime() が private static として存在するべき (タスク 8-2 / 要件 9-7)。");

            try
            {
                method.Invoke(null, null);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }
    }
}
