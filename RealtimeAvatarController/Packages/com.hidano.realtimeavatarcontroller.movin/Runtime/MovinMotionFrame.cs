using System;
using System.Collections.Generic;
using RealtimeAvatarController.Motion;

namespace RealtimeAvatarController.MoCap.Movin
{
    /// <summary>
    /// Immutable MOVIN motion frame keyed by received bone name.
    /// </summary>
    public sealed class MovinMotionFrame : MotionFrame
    {
        public override SkeletonType SkeletonType => SkeletonType.Generic;

        public IReadOnlyDictionary<string, MovinBonePose> Bones { get; }

        public MovinRootPose? RootPose { get; }

        public MovinMotionFrame(
            double timestamp,
            IReadOnlyDictionary<string, MovinBonePose> bones,
            MovinRootPose? rootPose = null)
            : base(timestamp)
        {
            Bones = bones ?? throw new ArgumentNullException(nameof(bones));
            RootPose = rootPose;
        }
    }
}
