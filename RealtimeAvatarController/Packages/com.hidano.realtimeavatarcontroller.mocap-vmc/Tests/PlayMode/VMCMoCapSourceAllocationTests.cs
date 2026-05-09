using System;
using System.Collections.Generic;
using NUnit.Framework;
using RealtimeAvatarController.Core;
using RealtimeAvatarController.Motion;
using UnityEngine;
using MotionFrame = RealtimeAvatarController.Core.MotionFrame;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    [TestFixture]
    public sealed class VMCMoCapSourceAllocationTests
    {
        private const int TestPort = 49534;
        private const int WarmupTicks = 16;
        private const int MeasuredTicks = 128;
        private const long MeasurementSlackBytes = 1024;

        private static object s_frameSink;

        private VMCMoCapSourceConfig _config;
        private VMCMoCapSource _source;
        private CountingObserver _observer;
        private IDisposable _subscription;

        [SetUp]
        public void SetUp()
        {
            RegistryLocator.ResetForTest();
            VMCSharedReceiver.ResetForTest();

            _config = ScriptableObject.CreateInstance<VMCMoCapSourceConfig>();
            _config.port = TestPort;
            _config.bindAddress = "0.0.0.0";

            _source = new VMCMoCapSource(
                slotId: "slot-vmc-alloc",
                errorChannel: RegistryLocator.ErrorChannel);
            _source.Initialize(_config);

            _observer = new CountingObserver();
            _subscription = _source.MotionStream.Subscribe(_observer);
        }

        [TearDown]
        public void TearDown()
        {
            _subscription?.Dispose();
            _subscription = null;

            _source?.Dispose();
            _source = null;

            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }

            s_frameSink = null;
            VMCSharedReceiver.ResetForTest();
            RegistryLocator.ResetForTest();
        }

        [Test]
        public void ForceTickForTest_AllocatesOnlyHumanoidMotionFramePerEmit_AfterWarmup()
        {
            WarmupForceTickPath();
            WarmupFrameAllocationBaseline();
            ForceFullCollection();

            var frameOnlyBytes = MeasureHumanoidFrameAllocations(MeasuredTicks);
            ForceFullCollection();

            _observer.Reset();
            var tickBytes = MeasureForceTickAllocations(MeasuredTicks);
            var extraBytes = tickBytes - frameOnlyBytes;

            Assert.That(_observer.NextCount, Is.EqualTo(MeasuredTicks));
            Assert.That(_observer.ErrorCount, Is.Zero);
            Assert.That(_observer.LastFrame, Is.InstanceOf<HumanoidMotionFrame>());
            Assert.That(
                extraBytes,
                Is.LessThanOrEqualTo(MeasurementSlackBytes),
                $"ForceTickForTest allocated {tickBytes} bytes for {MeasuredTicks} emits; "
                + $"HumanoidMotionFrame baseline was {frameOnlyBytes} bytes. "
                + "The application Tick path should add no per-emit allocation beyond the frame object.");
        }

        private void WarmupForceTickPath()
        {
            for (var i = 0; i < WarmupTicks; i++)
            {
                EmitInjectedFrame();
            }

            _observer.Reset();
        }

        private static void WarmupFrameAllocationBaseline()
        {
            s_frameSink = new HumanoidMotionFrame(
                0d,
                Array.Empty<float>(),
                Vector3.zero,
                Quaternion.identity,
                EmptyRotations.Instance);
        }

        private static long MeasureHumanoidFrameAllocations(int ticks)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < ticks; i++)
            {
                s_frameSink = new HumanoidMotionFrame(
                    i,
                    Array.Empty<float>(),
                    Vector3.zero,
                    Quaternion.identity,
                    EmptyRotations.Instance);
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private long MeasureForceTickAllocations(int ticks)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < ticks; i++)
            {
                EmitInjectedFrame();
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private void EmitInjectedFrame()
        {
            _source.InjectBoneRotationForTest(HumanBodyBones.LeftHand, Quaternion.identity);
            _source.ForceTickForTest();
        }

        private static void ForceFullCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private sealed class CountingObserver : IObserver<MotionFrame>
        {
            public int NextCount { get; private set; }

            public int ErrorCount { get; private set; }

            public MotionFrame LastFrame { get; private set; }

            public void Reset()
            {
                NextCount = 0;
                ErrorCount = 0;
                LastFrame = null;
            }

            public void OnNext(MotionFrame value)
            {
                NextCount++;
                LastFrame = value;
            }

            public void OnError(Exception error)
            {
                ErrorCount++;
            }

            public void OnCompleted()
            {
            }
        }

        private sealed class EmptyRotations : IReadOnlyDictionary<HumanBodyBones, Quaternion>
        {
            public static readonly EmptyRotations Instance = new EmptyRotations();

            public int Count => 0;

            public IEnumerable<HumanBodyBones> Keys => Array.Empty<HumanBodyBones>();

            public IEnumerable<Quaternion> Values => Array.Empty<Quaternion>();

            public Quaternion this[HumanBodyBones key] => throw new KeyNotFoundException();

            public bool ContainsKey(HumanBodyBones key) => false;

            public bool TryGetValue(HumanBodyBones key, out Quaternion value)
            {
                value = default;
                return false;
            }

            public IEnumerator<KeyValuePair<HumanBodyBones, Quaternion>> GetEnumerator()
            {
                return EmptyEnumerator.Instance;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return EmptyEnumerator.Instance;
            }
        }

        private sealed class EmptyEnumerator : IEnumerator<KeyValuePair<HumanBodyBones, Quaternion>>
        {
            public static readonly EmptyEnumerator Instance = new EmptyEnumerator();

            public KeyValuePair<HumanBodyBones, Quaternion> Current => default;

            object System.Collections.IEnumerator.Current => Current;

            public bool MoveNext() => false;

            public void Reset()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
