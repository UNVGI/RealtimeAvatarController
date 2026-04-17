using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UniRx;
using UnityEngine.TestTools;

namespace RealtimeAvatarController.Core.Tests
{
    [TestFixture]
    public class DefaultSlotErrorChannelTests
    {
        private DefaultSlotErrorChannel _channel;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _channel = new DefaultSlotErrorChannel();
            // Publish() 内部の Debug.LogError は本テストの検証対象ではない
            // (抑制ロジックの検証は SlotErrorChannelTests で LogAssert.Expect により行う)。
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            RegistryLocator.ResetForTest();
        }

        [Test]
        public void DefaultSlotErrorChannel_IsInternalSealed()
        {
            var type = typeof(DefaultSlotErrorChannel);
            Assert.That(type.IsSealed, Is.True);
            Assert.That(type.IsPublic, Is.False, "DefaultSlotErrorChannel should be internal");
        }

        [Test]
        public void DefaultSlotErrorChannel_ImplementsISlotErrorChannel()
        {
            Assert.That(_channel, Is.InstanceOf<ISlotErrorChannel>());
        }

        [Test]
        public void Errors_IsNotNull()
        {
            Assert.That(_channel.Errors, Is.Not.Null);
        }

        [Test]
        public void Publish_SendsEventToErrorsStream()
        {
            var received = new List<SlotError>();
            _channel.Errors.Subscribe(e => received.Add(e));

            var error = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            _channel.Publish(error);

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0], Is.SameAs(error));
        }

        [Test]
        public void Publish_MultipleErrors_AllReceivedByStream()
        {
            var received = new List<SlotError>();
            _channel.Errors.Subscribe(e => received.Add(e));

            var error1 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error2 = new SlotError("slot-2", SlotErrorCategory.ApplyFailure, null, DateTime.UtcNow);
            _channel.Publish(error1);
            _channel.Publish(error2);

            Assert.That(received.Count, Is.EqualTo(2));
            Assert.That(received[0], Is.SameAs(error1));
            Assert.That(received[1], Is.SameAs(error2));
        }

        [Test]
        public void Publish_SameSlotIdAndCategory_SecondTime_DebugLogErrorIsSuppressed()
        {
            var error1 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error2 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);

            // 初回 Publish: s_suppressedErrors に追加される (Add returns true → Debug.LogError 出力)
            _channel.Publish(error1);
            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-1", SlotErrorCategory.InitFailure)),
                Is.True,
                "After first Publish, key should be in suppressedErrors");

            // 2 回目 Publish: Add returns false → Debug.LogError は抑制される
            // ストリームには両方とも流れるが、Debug.LogError は 1 回のみ
            var received = new List<SlotError>();
            _channel.Errors.Subscribe(e => received.Add(e));
            _channel.Publish(error2);

            Assert.That(received.Count, Is.EqualTo(1), "Stream should still receive the second error");
        }

        [Test]
        public void Publish_DifferentCategory_BothLogErrors()
        {
            var error1 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error2 = new SlotError("slot-1", SlotErrorCategory.ApplyFailure, null, DateTime.UtcNow);

            _channel.Publish(error1);
            _channel.Publish(error2);

            // 異なるカテゴリなので両方とも s_suppressedErrors に存在する
            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-1", SlotErrorCategory.InitFailure)),
                Is.True);
            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-1", SlotErrorCategory.ApplyFailure)),
                Is.True);
        }

        [Test]
        public void Publish_DifferentSlotId_BothLogErrors()
        {
            var error1 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error2 = new SlotError("slot-2", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);

            _channel.Publish(error1);
            _channel.Publish(error2);

            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-1", SlotErrorCategory.InitFailure)),
                Is.True);
            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-2", SlotErrorCategory.InitFailure)),
                Is.True);
        }

        [Test]
        public void ResetForTest_ClearsSuppressedErrors()
        {
            var error = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            _channel.Publish(error);

            Assert.That(RegistryLocator.s_suppressedErrors.Count, Is.GreaterThan(0));

            RegistryLocator.ResetForTest();

            Assert.That(RegistryLocator.s_suppressedErrors.Count, Is.EqualTo(0),
                "After ResetForTest, suppressedErrors should be cleared");
        }

        [Test]
        public void ResetForTest_AfterReset_SameKeyLogsAgain()
        {
            var error = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            _channel.Publish(error);

            // key は s_suppressedErrors に存在する
            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-1", SlotErrorCategory.InitFailure)),
                Is.True);

            RegistryLocator.ResetForTest();
            _channel = new DefaultSlotErrorChannel();

            // リセット後、同一キーの Publish で再び s_suppressedErrors に追加される
            _channel.Publish(error);
            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-1", SlotErrorCategory.InitFailure)),
                Is.True,
                "After ResetForTest, the same key should be re-added (meaning Debug.LogError fires again)");
        }

        [Test]
        public void Publish_StreamReceivesAllEvents_RegardlessOfSuppression()
        {
            var received = new List<SlotError>();
            _channel.Errors.Subscribe(e => received.Add(e));

            var error1 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error2 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error3 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);

            _channel.Publish(error1);
            _channel.Publish(error2);
            _channel.Publish(error3);

            // ストリームには抑制なく全イベントが流れる
            Assert.That(received.Count, Is.EqualTo(3));
        }
    }
}
