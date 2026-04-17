using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace RealtimeAvatarController.MoCap.VMC.Tests
{
    /// <summary>
    /// <c>mocap-vmc</c> PlayMode 統合テスト用の UDP + OSC 送信テストダブル
    /// (tasks.md タスク 11-1, design.md §15.2, requirements.md 要件 10-3)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>役割</b>: 実 VMC 送信ソース (バーチャルモーションキャプチャ等) の代替として、
    /// ローカルホスト (<see cref="IPAddress.Loopback"/>) 上の指定ポートへ
    /// OSC 1.0 互換バイト列を UDP 送信する。<c>VmcMoCapSourceIntegrationTests</c> から
    /// <see cref="VmcMoCapSource"/> への End-to-End 入力経路をテストするために用いる。
    /// </para>
    /// <para>
    /// <b>なぜ <c>uOscClient</c> を使わず直接 UDP 送信するか</b>:
    /// テスト対象である <c>com.hidano.uosc</c> 受信系を実ネットワーク経路で検証するため、
    /// 送信側は uOSC 実装に依存せず独立した参照実装 (素の <see cref="UdpClient"/> +
    /// OSC 1.0 エンコード) を用意する。これにより「送受信の両端が同一バグで相殺される」
    /// 相互依存リスクを排除する。
    /// </para>
    /// <para>
    /// <b>使用方法</b>: テストクラスの <c>[SetUp]</c> で生成し、<c>[TearDown]</c> で
    /// <see cref="Dispose"/> する。PlayMode テスト本体での送信呼び出しは以下を想定する:
    /// <list type="bullet">
    ///   <item><see cref="SendRootPos"/>: <c>/VMC/Ext/Root/Pos</c> 相当のパケット送信</item>
    ///   <item><see cref="SendBonePos"/>: <c>/VMC/Ext/Bone/Pos</c> 相当のパケット送信</item>
    ///   <item><see cref="SendInvalidPacket"/>: 不正バイト列を送信 (パースエラーテスト用)</item>
    /// </list>
    /// </para>
    /// </remarks>
    internal sealed class UdpOscSenderTestDouble : IDisposable
    {
        private readonly UdpClient _sender;
        private readonly IPEndPoint _target;

        /// <summary>
        /// <see cref="UdpOscSenderTestDouble"/> を生成し <see cref="IPAddress.Loopback"/>:<paramref name="targetPort"/>
        /// への送信クライアントを準備する。
        /// </summary>
        /// <param name="targetPort">送信先の UDP ポート番号 (テスト側で <see cref="VmcMoCapSource"/> が bind しているポート)。</param>
        public UdpOscSenderTestDouble(int targetPort)
        {
            _sender = new UdpClient();
            _target = new IPEndPoint(IPAddress.Loopback, targetPort);
        }

        /// <summary>
        /// <c>/VMC/Ext/Root/Pos</c> OSC メッセージを送信する (design.md §3 / §7.2)。
        /// </summary>
        /// <param name="position">ルート位置。</param>
        /// <param name="rotation">ルート回転。</param>
        /// <remarks>
        /// VMC プロトコルで <c>/VMC/Ext/Root/Pos</c> の第 1 引数は任意名称文字列であり、
        /// <see cref="Internal.VmcFrameBuilder"/> では参照しない (design.md §7.2)。
        /// 固定値 <c>"root"</c> を送る。
        /// </remarks>
        public void SendRootPos(Vector3 position, Quaternion rotation)
        {
            var data = EncodeBoneLikeMessage("/VMC/Ext/Root/Pos", "root", position, rotation);
            _sender.Send(data, data.Length, _target);
        }

        /// <summary>
        /// <c>/VMC/Ext/Bone/Pos</c> OSC メッセージを送信する (design.md §3 / §6.3)。
        /// </summary>
        /// <param name="boneName">VMC ボーン名 (<see cref="UnityEngine.HumanBodyBones"/> 列挙値名と一致させる)。</param>
        /// <param name="position">ボーン位置。</param>
        /// <param name="rotation">ボーン回転。</param>
        public void SendBonePos(string boneName, Vector3 position, Quaternion rotation)
        {
            var data = EncodeBoneLikeMessage("/VMC/Ext/Bone/Pos", boneName, position, rotation);
            _sender.Send(data, data.Length, _target);
        }

        /// <summary>
        /// OSC として解釈できない不正バイト列を送信する (パースエラー / ストリーム継続性テスト用)。
        /// </summary>
        /// <remarks>
        /// OSC の最初のフィールドは必ず <c>'/'</c> で始まるアドレス文字列であるため、
        /// 先頭を <c>0xFF</c> 系のバイト列にすることで uOSC 側のパースを失敗させる。
        /// </remarks>
        public void SendInvalidPacket()
        {
            byte[] garbage = { 0xFF, 0xFE, 0xFD, 0xFC, 0x01, 0x02, 0x03, 0x04 };
            _sender.Send(garbage, garbage.Length, _target);
        }

        /// <summary>UDP クライアントを解放する。二重呼び出しは <see cref="UdpClient.Dispose"/> 側で吸収される。</summary>
        public void Dispose() => _sender.Dispose();

        /// <summary>
        /// <c>/VMC/Ext/Root/Pos</c> / <c>/VMC/Ext/Bone/Pos</c> 共通の
        /// <c>(string name, float px, float py, float pz, float qx, float qy, float qz, float qw)</c>
        /// 引数構造を OSC 1.0 バイト列にエンコードする。
        /// </summary>
        private static byte[] EncodeBoneLikeMessage(string address, string name, Vector3 position, Quaternion rotation)
        {
            using (var ms = new MemoryStream())
            {
                WriteOscString(ms, address);
                WriteOscString(ms, ",sfffffff");
                WriteOscString(ms, name);
                WriteFloat(ms, position.x);
                WriteFloat(ms, position.y);
                WriteFloat(ms, position.z);
                WriteFloat(ms, rotation.x);
                WriteFloat(ms, rotation.y);
                WriteFloat(ms, rotation.z);
                WriteFloat(ms, rotation.w);
                return ms.ToArray();
            }
        }

        /// <summary>OSC 1.0 文字列 (null 終端 + 4 バイトアラインパディング) を書き出す。</summary>
        private static void WriteOscString(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
            int total = bytes.Length + 1;
            int padding = (4 - (total % 4)) % 4;
            for (int i = 0; i < padding; i++)
            {
                stream.WriteByte(0);
            }
        }

        /// <summary>OSC 1.0 float32 (ビッグエンディアン 4 バイト IEEE 754) を書き出す。</summary>
        private static void WriteFloat(Stream stream, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
