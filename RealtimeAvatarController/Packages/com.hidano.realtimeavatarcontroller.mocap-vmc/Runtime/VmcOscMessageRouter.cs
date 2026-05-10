using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC
{
    public interface IVmcBoneRotationWriter
    {
        void WriteBoneRotation(HumanBodyBones bone, Quaternion rotation);

        void WriteRoot(Vector3 position, Quaternion rotation);
    }

    internal static class VmcOscMessageRouter
    {
        private const string AddressBonePos = "/VMC/Ext/Bone/Pos";
        private const string AddressRootPos = "/VMC/Ext/Root/Pos";

        internal static void RouteMessage(in uOSC.Message message, IVmcBoneRotationWriter writer)
        {
            if (writer == null)
            {
                return;
            }

            if (message.address == AddressBonePos)
            {
                if (TryParseBoneMessage(in message, out var bone, out var rotation))
                {
                    writer.WriteBoneRotation(bone, rotation);
                }
            }
            else if (message.address == AddressRootPos)
            {
                if (TryParseRootMessage(in message, out var position, out var rotation))
                {
                    writer.WriteRoot(position, rotation);
                }
            }
        }

        internal static bool TryParseBoneMessage(
            in uOSC.Message message,
            out HumanBodyBones bone,
            out Quaternion rotation)
        {
            bone = default;
            rotation = default;

            var values = message.values;
            if (values == null || values.Length != 8)
            {
                return false;
            }

            if (!(values[0] is string boneName) ||
                !(values[4] is float rotX) ||
                !(values[5] is float rotY) ||
                !(values[6] is float rotZ) ||
                !(values[7] is float rotW))
            {
                return false;
            }

            if (!(values[1] is float) ||
                !(values[2] is float) ||
                !(values[3] is float))
            {
                return false;
            }

            if (!VmcBoneNameMap.TryGetValue(boneName, out var parsedBone))
            {
                return false;
            }

            bone = parsedBone;
            rotation = new Quaternion(rotX, rotY, rotZ, rotW);
            return true;
        }

        internal static bool TryParseRootMessage(
            in uOSC.Message message,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = default;
            rotation = default;

            var values = message.values;
            if (values == null || (values.Length != 8 && values.Length != 14))
            {
                return false;
            }

            if (!(values[0] is string) ||
                !(values[1] is float posX) ||
                !(values[2] is float posY) ||
                !(values[3] is float posZ) ||
                !(values[4] is float rotX) ||
                !(values[5] is float rotY) ||
                !(values[6] is float rotZ) ||
                !(values[7] is float rotW))
            {
                return false;
            }

            position = new Vector3(posX, posY, posZ);
            rotation = new Quaternion(rotX, rotY, rotZ, rotW);
            return true;
        }
    }
}
