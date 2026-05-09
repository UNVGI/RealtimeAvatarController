using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC
{
    internal interface IVmcBoneRotationWriter
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
                RouteBonePosition(in message, writer);
            }
            else if (message.address == AddressRootPos)
            {
                RouteRootPosition(in message, writer);
            }
        }

        private static void RouteBonePosition(in uOSC.Message message, IVmcBoneRotationWriter writer)
        {
            var values = message.values;
            if (values == null || values.Length != 8)
            {
                return;
            }

            if (!(values[0] is string boneName) ||
                !(values[4] is float rotX) ||
                !(values[5] is float rotY) ||
                !(values[6] is float rotZ) ||
                !(values[7] is float rotW))
            {
                return;
            }

            if (!(values[1] is float) ||
                !(values[2] is float) ||
                !(values[3] is float))
            {
                return;
            }

            if (!VmcBoneNameMap.TryGetValue(boneName, out var bone))
            {
                return;
            }

            writer.WriteBoneRotation(bone, new Quaternion(rotX, rotY, rotZ, rotW));
        }

        private static void RouteRootPosition(in uOSC.Message message, IVmcBoneRotationWriter writer)
        {
            var values = message.values;
            if (values == null || (values.Length != 8 && values.Length != 14))
            {
                return;
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
                return;
            }

            writer.WriteRoot(
                new Vector3(posX, posY, posZ),
                new Quaternion(rotX, rotY, rotZ, rotW));
        }
    }
}
