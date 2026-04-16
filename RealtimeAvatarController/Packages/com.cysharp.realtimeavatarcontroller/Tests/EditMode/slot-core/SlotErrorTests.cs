using System;
using NUnit.Framework;
using RealtimeAvatarController.Core;

namespace RealtimeAvatarController.Core.Tests
{
    public class SlotErrorTests
    {
        [Test]
        public void SlotError_IsSealed()
        {
            Assert.That(typeof(SlotError).IsSealed, Is.True);
        }

        [Test]
        public void Constructor_SetsAllProperties()
        {
            var exception = new InvalidOperationException("test error");
            var timestamp = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

            var error = new SlotError("slot-1", SlotErrorCategory.InitFailure, exception, timestamp);

            Assert.That(error.SlotId, Is.EqualTo("slot-1"));
            Assert.That(error.Category, Is.EqualTo(SlotErrorCategory.InitFailure));
            Assert.That(error.Exception, Is.SameAs(exception));
            Assert.That(error.Timestamp, Is.EqualTo(timestamp));
        }

        [Test]
        public void Constructor_WithNullException_SetsExceptionToNull()
        {
            var timestamp = DateTime.UtcNow;

            var error = new SlotError("slot-2", SlotErrorCategory.VmcReceive, null, timestamp);

            Assert.That(error.SlotId, Is.EqualTo("slot-2"));
            Assert.That(error.Category, Is.EqualTo(SlotErrorCategory.VmcReceive));
            Assert.That(error.Exception, Is.Null);
            Assert.That(error.Timestamp, Is.EqualTo(timestamp));
        }

        [Test]
        public void Constructor_AllCategories_AreAccepted()
        {
            var timestamp = DateTime.UtcNow;

            var vmcError = new SlotError("s", SlotErrorCategory.VmcReceive, null, timestamp);
            var initError = new SlotError("s", SlotErrorCategory.InitFailure, null, timestamp);
            var applyError = new SlotError("s", SlotErrorCategory.ApplyFailure, null, timestamp);
            var conflictError = new SlotError("s", SlotErrorCategory.RegistryConflict, null, timestamp);

            Assert.That(vmcError.Category, Is.EqualTo(SlotErrorCategory.VmcReceive));
            Assert.That(initError.Category, Is.EqualTo(SlotErrorCategory.InitFailure));
            Assert.That(applyError.Category, Is.EqualTo(SlotErrorCategory.ApplyFailure));
            Assert.That(conflictError.Category, Is.EqualTo(SlotErrorCategory.RegistryConflict));
        }

        [Test]
        public void Properties_AreReadOnly_Immutable()
        {
            var exception = new Exception("immutable test");
            var timestamp = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

            var error = new SlotError("slot-immutable", SlotErrorCategory.ApplyFailure, exception, timestamp);

            // プロパティが getter-only であることを確認 (setter が存在しないことをリフレクションで検証)
            var slotIdProp = typeof(SlotError).GetProperty(nameof(SlotError.SlotId));
            var categoryProp = typeof(SlotError).GetProperty(nameof(SlotError.Category));
            var exceptionProp = typeof(SlotError).GetProperty(nameof(SlotError.Exception));
            var timestampProp = typeof(SlotError).GetProperty(nameof(SlotError.Timestamp));

            Assert.That(slotIdProp.CanWrite, Is.False, "SlotId should be read-only");
            Assert.That(categoryProp.CanWrite, Is.False, "Category should be read-only");
            Assert.That(exceptionProp.CanWrite, Is.False, "Exception should be read-only");
            Assert.That(timestampProp.CanWrite, Is.False, "Timestamp should be read-only");
        }

        [Test]
        public void Constructor_WithEmptySlotId_IsAllowed()
        {
            var error = new SlotError("", SlotErrorCategory.RegistryConflict, null, DateTime.UtcNow);

            Assert.That(error.SlotId, Is.EqualTo(""));
        }
    }
}
