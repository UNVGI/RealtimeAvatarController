using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// SlotRegistry の EditMode テスト。
    /// テスト観点:
    ///   - AddSlot 成功 (Dictionary に登録され GetSlot で取得可能)
    ///   - 同一 slotId 重複追加で InvalidOperationException
    ///   - RemoveSlot 成功
    ///   - 未登録 slotId の RemoveSlot で InvalidOperationException
    ///   - GetSlot 成功 / 未登録時 null
    ///   - GetAllSlots (空・複数登録後)
    /// Requirements: 2.1, 2.2, 2.3, 2.4, 2.5
    /// </summary>
    [TestFixture]
    public class SlotRegistryTests
    {
        private SlotRegistry _registry;
        private SlotSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _registry = new SlotRegistry();
            _settings = ScriptableObject.CreateInstance<SlotSettings>();
            _settings.slotId = "slot-1";
            _settings.displayName = "Slot 1";
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null)
                UnityEngine.Object.DestroyImmediate(_settings);
        }

        // --- AddSlot: 追加 (Req 2.1) ---

        [Test]
        public void AddSlot_NewSlotId_RegistersSuccessfully()
        {
            Assert.DoesNotThrow(() => _registry.AddSlot("slot-1", _settings));

            var handle = _registry.GetSlot("slot-1");
            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.SlotId, Is.EqualTo("slot-1"));
            Assert.That(handle.DisplayName, Is.EqualTo("Slot 1"));
            Assert.That(handle.State, Is.EqualTo(SlotState.Created));
            Assert.That(handle.Settings, Is.SameAs(_settings));
        }

        // --- AddSlot: 重複追加エラー (Req 2.3) ---

        [Test]
        public void AddSlot_DuplicateSlotId_ThrowsInvalidOperationException()
        {
            _registry.AddSlot("slot-1", _settings);

            var duplicate = ScriptableObject.CreateInstance<SlotSettings>();
            duplicate.slotId = "slot-1";
            duplicate.displayName = "Duplicate";
            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => _registry.AddSlot("slot-1", duplicate));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public void AddSlot_DuplicateSlotId_DoesNotOverwriteExistingHandle()
        {
            _registry.AddSlot("slot-1", _settings);
            var originalHandle = _registry.GetSlot("slot-1");

            var duplicate = ScriptableObject.CreateInstance<SlotSettings>();
            duplicate.slotId = "slot-1";
            duplicate.displayName = "Overwrite Attempt";
            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => _registry.AddSlot("slot-1", duplicate));

                var afterHandle = _registry.GetSlot("slot-1");
                Assert.That(afterHandle, Is.SameAs(originalHandle));
                Assert.That(afterHandle.Settings, Is.SameAs(_settings));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
            }
        }

        // --- RemoveSlot: 削除 (Req 2.2) ---

        [Test]
        public void RemoveSlot_ExistingSlotId_RemovesHandle()
        {
            _registry.AddSlot("slot-1", _settings);

            Assert.DoesNotThrow(() => _registry.RemoveSlot("slot-1"));
            Assert.That(_registry.GetSlot("slot-1"), Is.Null);
        }

        // --- RemoveSlot: 未登録削除エラー ---

        [Test]
        public void RemoveSlot_UnregisteredSlotId_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _registry.RemoveSlot("unknown"));
        }

        [Test]
        public void RemoveSlot_CalledTwice_SecondThrowsInvalidOperationException()
        {
            _registry.AddSlot("slot-1", _settings);
            _registry.RemoveSlot("slot-1");

            Assert.Throws<InvalidOperationException>(() => _registry.RemoveSlot("slot-1"));
        }

        // --- GetSlot 成功 / 失敗 (Req 2.5) ---

        [Test]
        public void GetSlot_RegisteredSlotId_ReturnsHandle()
        {
            _registry.AddSlot("slot-1", _settings);

            var handle = _registry.GetSlot("slot-1");
            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.SlotId, Is.EqualTo("slot-1"));
        }

        [Test]
        public void GetSlot_UnregisteredSlotId_ReturnsNull()
        {
            var handle = _registry.GetSlot("unknown");
            Assert.That(handle, Is.Null);
        }

        // --- GetAllSlots (Req 2.4) ---

        [Test]
        public void GetAllSlots_Empty_ReturnsEmptyList()
        {
            var all = _registry.GetAllSlots();
            Assert.That(all, Is.Not.Null);
            Assert.That(all, Is.Empty);
        }

        [Test]
        public void GetAllSlots_AfterRegistrations_ReturnsAllHandles()
        {
            var settings2 = ScriptableObject.CreateInstance<SlotSettings>();
            settings2.slotId = "slot-2";
            settings2.displayName = "Slot 2";
            try
            {
                _registry.AddSlot("slot-1", _settings);
                _registry.AddSlot("slot-2", settings2);

                var all = _registry.GetAllSlots();
                Assert.That(all, Has.Count.EqualTo(2));

                var slotIds = all.Select(h => h.SlotId).ToList();
                Assert.That(slotIds, Contains.Item("slot-1"));
                Assert.That(slotIds, Contains.Item("slot-2"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings2);
            }
        }

        [Test]
        public void GetAllSlots_ReturnsReadOnlyList()
        {
            _registry.AddSlot("slot-1", _settings);

            var all = _registry.GetAllSlots();
            Assert.That(all, Is.InstanceOf<IReadOnlyList<SlotHandle>>());
        }

        [Test]
        public void GetAllSlots_AfterRemove_ExcludesRemovedHandle()
        {
            var settings2 = ScriptableObject.CreateInstance<SlotSettings>();
            settings2.slotId = "slot-2";
            settings2.displayName = "Slot 2";
            try
            {
                _registry.AddSlot("slot-1", _settings);
                _registry.AddSlot("slot-2", settings2);
                _registry.RemoveSlot("slot-1");

                var all = _registry.GetAllSlots();
                Assert.That(all, Has.Count.EqualTo(1));
                Assert.That(all[0].SlotId, Is.EqualTo("slot-2"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings2);
            }
        }

        // --- validation-design.md [N-3] / Task 15.2: internal sealed スコープの徹底確認 ---

        [Test]
        public void SlotRegistry_Type_IsNotPublic()
        {
            var type = typeof(SlotRegistry);
            Assert.That(type.IsPublic, Is.False,
                "SlotRegistry はパッケージ外から参照されないよう internal でなければならない (validation-design.md [N-3])");
            Assert.That(type.IsNotPublic, Is.True,
                "SlotRegistry の可視性は internal (IsNotPublic == true) である必要がある");
        }

        [Test]
        public void SlotRegistry_Type_IsSealed()
        {
            var type = typeof(SlotRegistry);
            Assert.That(type.IsSealed, Is.True,
                "SlotRegistry は継承拡張を禁止するため sealed でなければならない (validation-design.md [N-3])");
        }

        [Test]
        public void SlotRegistry_Type_IsVisibleToTestsViaInternalsVisibleTo()
        {
            var type = typeof(SlotRegistry);
            Assert.That(type, Is.Not.Null,
                "InternalsVisibleTo 設定 (タスク 8.3) により SlotRegistry がテストアセンブリから参照可能であること");
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("RealtimeAvatarController.Core"),
                "SlotRegistry は Core アセンブリに配置され、テストは InternalsVisibleTo 経由でのみアクセス可能");
        }
    }
}
