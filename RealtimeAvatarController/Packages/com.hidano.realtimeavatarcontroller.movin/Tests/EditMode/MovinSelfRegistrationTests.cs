using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using UniRx;

namespace RealtimeAvatarController.MoCap.Movin.Tests
{
    [TestFixture]
    public class MovinSelfRegistrationTests
    {
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

        [Test]
        public void RegisterRuntime_AddsMovinTypeId_ToMoCapSourceRegistry()
        {
            InvokeRegisterRuntime();

            var typeIds = RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds();

            CollectionAssert.Contains(typeIds, MovinMoCapSourceFactory.MovinSourceTypeId);
        }

        [Test]
        public void RegisterEditor_AddsMovinTypeId_ToMoCapSourceRegistry()
        {
            InvokeRegisterEditor();

            var typeIds = RegistryLocator.MoCapSourceRegistry.GetRegisteredTypeIds();

            CollectionAssert.Contains(typeIds, MovinMoCapSourceFactory.MovinSourceTypeId);
        }

        [Test]
        public void Register_DuplicateMovinTypeId_ThrowsRegistryConflictException()
        {
            var registry = RegistryLocator.MoCapSourceRegistry;
            registry.Register(
                MovinMoCapSourceFactory.MovinSourceTypeId,
                new MovinMoCapSourceFactory());

            var ex = Assert.Throws<RegistryConflictException>(
                () => registry.Register(
                    MovinMoCapSourceFactory.MovinSourceTypeId,
                    new MovinMoCapSourceFactory()));

            Assert.That(ex.TypeId, Is.EqualTo(MovinMoCapSourceFactory.MovinSourceTypeId));
            Assert.That(ex.RegistryName, Is.EqualTo("IMoCapSourceRegistry"));
        }

        [Test]
        public void RegisterRuntime_WhenMovinAlreadyRegistered_PublishesRegistryConflict()
        {
            var errorChannel = new CapturingSlotErrorChannel();
            RegistryLocator.OverrideErrorChannel(errorChannel);
            RegistryLocator.MoCapSourceRegistry.Register(
                MovinMoCapSourceFactory.MovinSourceTypeId,
                new MovinMoCapSourceFactory());

            InvokeRegisterRuntime();

            AssertRegistryConflictPublished(errorChannel);
        }

        [Test]
        public void RegisterEditor_WhenMovinAlreadyRegistered_PublishesRegistryConflict()
        {
            var errorChannel = new CapturingSlotErrorChannel();
            RegistryLocator.OverrideErrorChannel(errorChannel);
            RegistryLocator.MoCapSourceRegistry.Register(
                MovinMoCapSourceFactory.MovinSourceTypeId,
                new MovinMoCapSourceFactory());

            InvokeRegisterEditor();

            AssertRegistryConflictPublished(errorChannel);
        }

        private static void AssertRegistryConflictPublished(CapturingSlotErrorChannel errorChannel)
        {
            Assert.That(errorChannel.PublishedErrors, Has.Count.EqualTo(1));

            var error = errorChannel.PublishedErrors[0];
            Assert.That(error.SlotId, Is.EqualTo(MovinMoCapSourceFactory.MovinSourceTypeId));
            Assert.That(error.Category, Is.EqualTo(SlotErrorCategory.RegistryConflict));
            Assert.That(error.Exception, Is.InstanceOf<RegistryConflictException>());

            var conflict = (RegistryConflictException)error.Exception;
            Assert.That(conflict.TypeId, Is.EqualTo(MovinMoCapSourceFactory.MovinSourceTypeId));
            Assert.That(conflict.RegistryName, Is.EqualTo("IMoCapSourceRegistry"));
        }

        private static void InvokeRegisterRuntime()
        {
            InvokePrivateStaticMethod(typeof(MovinMoCapSourceFactory), "RegisterRuntime");
        }

        private static void InvokeRegisterEditor()
        {
            var registrarType = Type.GetType(
                "RealtimeAvatarController.MoCap.Movin.Editor.MovinMoCapSourceFactoryEditorRegistrar, RealtimeAvatarController.MoCap.Movin.Editor");

            Assert.That(registrarType, Is.Not.Null);
            InvokePrivateStaticMethod(registrarType, "RegisterEditor");
        }

        private static void InvokePrivateStaticMethod(Type type, string methodName)
        {
            var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            try
            {
                method.Invoke(null, null);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private sealed class CapturingSlotErrorChannel : ISlotErrorChannel
        {
            private readonly Subject<SlotError> _errors = new Subject<SlotError>();

            public List<SlotError> PublishedErrors { get; } = new List<SlotError>();

            public IObservable<SlotError> Errors => _errors;

            public void Publish(SlotError error)
            {
                PublishedErrors.Add(error);
                _errors.OnNext(error);
            }
        }
    }
}
