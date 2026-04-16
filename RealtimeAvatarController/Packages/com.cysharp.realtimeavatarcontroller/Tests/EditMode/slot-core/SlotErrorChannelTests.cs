using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealtimeAvatarController.Core.Tests
{
    /// <summary>
    /// エラーチャネル (ISlotErrorChannel / DefaultSlotErrorChannel) の EditMode テスト。
    /// テスト観点:
    ///   - Publish() 後に Errors ストリームでイベント受信
    ///   - 同一キーの 2 回目発行では Debug.LogError カウントが増えないこと (LogAssert で検証)
    ///   - RegistryLocator.ResetForTest() 後に抑制 HashSet がクリアされること
    /// Requirements: 12.5, 12.6, 12.7, 14.3
    /// </summary>
    [TestFixture]
    public class SlotErrorChannelTests
    {
        private DefaultSlotErrorChannel _channel;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            _channel = new DefaultSlotErrorChannel();
        }

        [TearDown]
        public void TearDown()
        {
            RegistryLocator.ResetForTest();
        }

        // --- Publish → Errors ストリーム受信 (Req 12.6) ---

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
        public void Publish_StreamReceivesAllEvents_RegardlessOfSuppression()
        {
            // Req 12.6: Errors ストリームへの発行は抑制なく行われる
            var received = new List<SlotError>();
            _channel.Errors.Subscribe(e => received.Add(e));

            var error1 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error2 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error3 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);

            _channel.Publish(error1);
            _channel.Publish(error2);
            _channel.Publish(error3);

            Assert.That(received.Count, Is.EqualTo(3), "Stream should receive all events regardless of Debug.LogError suppression");
        }

        // --- 同一キー 2 回目で Debug.LogError 抑制 (Req 12.5) ---

        [Test]
        public void Publish_FirstTime_OutputsDebugLogError()
        {
            // Req 12.5: 初回 Publish で Debug.LogError が出力される
            var error = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            _channel.Publish(error);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
        }

        [Test]
        public void Publish_SameSlotIdAndCategory_SecondTime_DebugLogErrorIsSuppressed()
        {
            // Req 12.5: 同一 (SlotId, Category) の 2 回目以降は Debug.LogError が抑制される
            var error1 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error2 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);

            _channel.Publish(error1);
            _channel.Publish(error2);

            // LogAssert.Expect で 1 回のみ期待: 2 回目は出力されない
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
            // 2 回目の LogError はないため Expect を 1 回のみ呼ぶ
        }

        [Test]
        public void Publish_SameKey_SuppressedErrors_ContainsKey()
        {
            var error = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            _channel.Publish(error);

            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-1", SlotErrorCategory.InitFailure)),
                Is.True,
                "After first Publish, key should be in suppressedErrors");

            // 2 回目は Add が false を返す (既に存在) → Debug.LogError は出力されない
            _channel.Publish(error);
            Assert.That(RegistryLocator.s_suppressedErrors.Count, Is.EqualTo(1),
                "suppressedErrors should still have only 1 entry for the same key");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
        }

        [Test]
        public void Publish_DifferentCategory_BothOutputDebugLogError()
        {
            // 異なるカテゴリは別キーとして扱われ、それぞれ Debug.LogError が出力される
            var error1 = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            var error2 = new SlotError("slot-1", SlotErrorCategory.ApplyFailure, null, DateTime.UtcNow);

            _channel.Publish(error1);
            _channel.Publish(error2);

            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-1", SlotErrorCategory.InitFailure)),
                Is.True);
            Assert.That(
                RegistryLocator.s_suppressedErrors.Contains(("slot-1", SlotErrorCategory.ApplyFailure)),
                Is.True);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*ApplyFailure"));
        }

        [Test]
        public void Publish_DifferentSlotId_BothOutputDebugLogError()
        {
            // 異なる SlotId は別キーとして扱われる
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

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-2.*InitFailure"));
        }

        // --- ResetForTest 後に抑制がリセットされる (Req 12.7) ---

        [Test]
        public void ResetForTest_ClearsSuppressedErrors()
        {
            var error = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            _channel.Publish(error);

            Assert.That(RegistryLocator.s_suppressedErrors.Count, Is.GreaterThan(0));

            RegistryLocator.ResetForTest();

            Assert.That(RegistryLocator.s_suppressedErrors.Count, Is.EqualTo(0),
                "After ResetForTest, suppressedErrors should be cleared");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
        }

        [Test]
        public void ResetForTest_AfterReset_SameKeyLogsAgain()
        {
            // Req 12.7: リセット後、同一キーの Publish で再び Debug.LogError が出力される
            var error = new SlotError("slot-1", SlotErrorCategory.InitFailure, null, DateTime.UtcNow);
            _channel.Publish(error);

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

            // 初回 + リセット後の初回で合計 2 回 Debug.LogError が出力される
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SlotError\].*slot-1.*InitFailure"));
        }
    }
}
