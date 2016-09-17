using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VrMMOServer
{
    class GamePacketCoordinator
    {
        private const UInt32 default_protocol = 0xDEADBEEF;
        private const UInt16 max_sequence = 65535;
        private const UInt16 max_bitfield = 32;
        private static Int64 packets_sent = 0;
        private static Int64 time_start = GameServer.getServerStopwatchMillis();
        private UInt16 next_sequence_to_send = 0;
        private UInt32 our_ack_bitfield;
        private UInt16 ack_sequence_id;

        private UInt16 their_ack_sequence_id; // Sequence ID of last packet ack'ed by other party
        private UInt32 their_ack_bitfield; // Bitfield of ack'ed packets by other party

        public OnlinePlayerEntity onlinePlayerEntity; // null on client

        public Int64 timeLastPacketSent;
        public Int64 timeLastPacketReceived;

        public bool readyForDisconnect;

        // Running in server mode
        public GamePacketCoordinator()
        {
            next_sequence_to_send = 0;
            our_ack_bitfield = 0;
            ack_sequence_id = 0;
            their_ack_sequence_id = 0;
            their_ack_bitfield = 0;
            readyForDisconnect = false;
            timeLastPacketReceived = Int64.MaxValue;
        }

        public bool boundToEntity(GameEntity ge)
        {
            return ge.id == onlinePlayerEntity.id;
        }

        public void setReadyForDisconnect()
        {
            readyForDisconnect = true;
        }

        public bool isReadyForDisconnect()
        {
            return readyForDisconnect;
        }

        public GamePacket parseIncomingPacket(Byte[] data)
        {
            NetworkDataReader ndr = new NetworkDataReader(data);

            // Check that protocol matches
            UInt32 protocol = ndr.getUInt32();
            if (protocol != default_protocol)
            {
                Console.WriteLine("Protocol error!");
            }

            // Log time packet received
            timeLastPacketReceived = GameServer.getServerStopwatchMillis();

            // Update our acknowledge sequence + bitfield
            UInt16 sequence_id = ndr.getUInt16();
            flagReceivedPacketAck(sequence_id);

            // Update their acknowledge sequence + bitfield
            UInt16 ack_sequence_id = ndr.getUInt16();
            UInt32 ack_bitfield = ndr.getUInt32();
            updateSentPacketAck(ack_sequence_id, ack_bitfield);

            // Find packet type and return the corresponding packet
            UInt16 packet_type = ndr.getUInt16();
            switch (packet_type)
            {
                case PingPacket.packet_type:
                    return PingPacket.fromData(ndr);
                case EntityUpdatePacket.packet_type:
                    return EntityUpdatePacket.fromData(ndr);
                case EntityRemovePacket.packet_type:
                    return EntityRemovePacket.fromData(ndr);
            }
            return null;
        }

        public void sendPacketToClient(UdpClient client, GamePacket gp)
        {
            sendPacket(client, gp, onlinePlayerEntity.ip);
        }

        public void sendPacketToServer(UdpClient client, GamePacket gp)
        {
            sendPacket(client, gp, null);
        }

        protected void sendPacket(UdpClient client, GamePacket gp, IPEndPoint e)
        {
            NetworkDataWriter ndw = new NetworkDataWriter();
            writeNextPacketHeader(ndw);
            gp.write(ndw);
            byte[] bytes = ndw.getByteArray();
            try
            {
                client.BeginSend(bytes, bytes.Length, e, new AsyncCallback(sentPacket), null);
                timeLastPacketSent = GameServer.getServerStopwatchMillis();
                packets_sent++;
            }
            catch (ObjectDisposedException err)
            {
                return;
            }
            catch (SocketException err)
            {
                return;
            }
        }

        private void sentPacket(IAsyncResult ia)
        {
        }

        public static Int64 packetsTotal()
        {
            return packets_sent;
        }

        public static Int64 packetsPerSecond()
        {
            if (GameServer.getServerStopwatchMillis() == time_start) return 0;
            return packets_sent * 1000 / (GameServer.getServerStopwatchMillis() - time_start);
        }

        private void writeNextPacketHeader(NetworkDataWriter ndw)
        {
            ndw.writeUInt32(default_protocol);
            ndw.writeUInt16(next_sequence_to_send);
            next_sequence_to_send++;
            ndw.writeUInt16(ack_sequence_id);
            ndw.writeUInt32(our_ack_bitfield);
        }

        public string getAckStatusString()
        {
            string status = "";
            status += "Receiving: " + ack_sequence_id.ToString() + " " + Convert.ToString(our_ack_bitfield, 2) + "\r\n";
            status += "Sending: " + their_ack_sequence_id.ToString() + " " + Convert.ToString(their_ack_bitfield, 2) + "\r\n";
            return status;
        }

        /**
            Flag the packet received from the other party as received for future acknowledgement
        */
        private void flagReceivedPacketAck(UInt16 sequence_id)
        {
            if (isMoreRecentSequence(sequence_id, ack_sequence_id))
            {
                int sequence_separation = getSequenceSeparation(sequence_id, ack_sequence_id);
                our_ack_bitfield = our_ack_bitfield << sequence_separation;
                our_ack_bitfield = our_ack_bitfield | ((UInt32)1 << sequence_separation - 1);
                ack_sequence_id = sequence_id;
            }
            else
            {
                int sequence_separation = getSequenceSeparation(ack_sequence_id, sequence_id);
                if (sequence_separation <= max_bitfield && sequence_separation > 0)
                {
                    our_ack_bitfield = our_ack_bitfield | ((UInt32)1 << sequence_separation - 1);
                }
            }
        }

        /**
            Update the received packet acknowledgement bitfield and id for the other party
        */
        private void updateSentPacketAck(UInt16 ack_sequence_id, UInt32 ack_bitfield)
        {
            if (isMoreRecentSequence(ack_sequence_id, their_ack_sequence_id))
            {
                their_ack_bitfield = ack_bitfield;
                their_ack_sequence_id = ack_sequence_id;
            }
        }


        public static bool isMoreRecentSequence(UInt16 s1, UInt16 s2)
        {
            return (s1 > s2) && (s1 - s2 <= max_sequence / 2) ||
                   (s2 > s1) && (s2 - s1 > max_sequence / 2);
        }

        public static int getSequenceSeparation(UInt16 more_recent, UInt16 less_recent)
        {
            if (more_recent >= less_recent)
            {
                return more_recent - less_recent;
            }
            else
            {
                return more_recent + max_sequence - less_recent + 1;
            }
        }
    }

    public abstract class GamePacket
    {
        public void write(NetworkDataWriter ndw)
        {
            ndw.writeUInt16(getPacketTypeCode());
            writePacketInfo(ndw);
        }

        abstract protected void writePacketInfo(NetworkDataWriter ndw);
        abstract protected UInt16 getPacketTypeCode();
    }

    public class PingPacket : GamePacket
    {
        public const UInt16 packet_type = 0x0000;

        public static PingPacket fromData(NetworkDataReader ndr)
        {
            return new PingPacket();
        }

        protected override UInt16 getPacketTypeCode()
        {
            return packet_type;
        }

        protected override void writePacketInfo(NetworkDataWriter ndw)
        {
            return;
        }
    }

    public class EntityRemovePacket : GamePacket
    {
        public const UInt16 packet_type = 0x0002;
        public UInt32 id;
        public static EntityRemovePacket fromData(NetworkDataReader ndr)
        {
            EntityRemovePacket erp = new EntityRemovePacket();
            erp.id = ndr.getUInt32();
            return erp;
        }

        protected override ushort getPacketTypeCode()
        {
            return packet_type;
        }

        protected override void writePacketInfo(NetworkDataWriter ndw)
        {
            ndw.writeUInt32(id);
        }

        internal static EntityRemovePacket fromEntity(GameEntity ge)
        {
            EntityRemovePacket erp = new EntityRemovePacket();
            erp.id = ge.id;
            return erp;
        }
    }

    public class EntityUpdatePacket : GamePacket
    {
        public Single x;
        public Single y;
        public Single upDown;
        public Single leftRight;
        public Single tilt;
        public UInt32 id;
        public UInt32 graphic;
        public const UInt16 packet_type = 0x0001;
        public static EntityUpdatePacket fromData(NetworkDataReader ndr)
        {
            EntityUpdatePacket eup = new EntityUpdatePacket();
            eup.x = ndr.getSingle();
            eup.y = ndr.getSingle();
            eup.upDown = ndr.getSingle();
            eup.leftRight = ndr.getSingle();
            eup.tilt = ndr.getSingle();
            eup.id = ndr.getUInt32();
            eup.graphic = ndr.getUInt32();
            return eup;
        }

        protected override UInt16 getPacketTypeCode()
        {
            return packet_type;
        }

        protected override void writePacketInfo(NetworkDataWriter ndw)
        {
            ndw.writeSingle(x);
            ndw.writeSingle(y);
            ndw.writeSingle(upDown);
            ndw.writeSingle(leftRight);
            ndw.writeSingle(tilt);
            ndw.writeUInt32(id);
            ndw.writeUInt32(graphic);
        }

        public override string ToString()
        {
            return "EntityUpdatePacket:\r\n"
                + " (" + x.ToString() + "," + y.ToString() + ")\r\n"
                + " (" + upDown.ToString() + "," + leftRight.ToString() + "," + tilt.ToString() + ")\r\n"
                + " (" + id.ToString() + "," + graphic.ToString() + ")";
        }

        public void update(GameEntity ge)
        {
            ge.x = x;
            ge.y = y;
            ge.upDown = upDown;
            ge.leftRight = leftRight;
            ge.tilt = tilt;
            ge.graphic = graphic;
        }

        internal static EntityUpdatePacket fromEntity(GameEntity ge)
        {
            EntityUpdatePacket eup = new EntityUpdatePacket();
            eup.x = ge.x;
            eup.y = ge.y;
            eup.graphic = ge.graphic;
            eup.id = ge.id;
            eup.leftRight = ge.leftRight;
            eup.upDown = ge.upDown;
            eup.tilt = ge.tilt;
            return eup;
        }
    }

    public class NetworkDataWriter
    {
        private MemoryStream ms;
        public NetworkDataWriter()
        {
            ms = new MemoryStream();
        }

        public byte[] getByteArray()
        {
            return ms.GetBuffer();
        }

        public void writeUInt16(UInt16 x)
        {
            byte[] bytes = System.BitConverter.GetBytes(x);
            NetworkDataReader.subarrayReverseOrder(bytes, 0, 2);
            ms.Write(bytes, 0, 2);
        }

        public void writeUInt32(UInt32 x)
        {
            byte[] bytes = System.BitConverter.GetBytes(x);
            NetworkDataReader.subarrayReverseOrder(bytes, 0, 4);
            ms.Write(bytes, 0, 4);
        }

        public void writeUInt64(UInt64 x)
        {
            byte[] bytes = System.BitConverter.GetBytes(x);
            NetworkDataReader.subarrayReverseOrder(bytes, 0, 8);
            ms.Write(bytes, 0, 8);
        }

        public void writeSingle(Single x)
        {
            byte[] bytes = System.BitConverter.GetBytes(x);
            NetworkDataReader.subarrayReverseOrder(bytes, 0, 4);
            ms.Write(bytes, 0, 4);
        }

    }

    public class NetworkDataReader
    {
        private Byte[] data;
        private int index;

        public NetworkDataReader(Byte[] d)
        {
            data = d;
            index = 0;
        }

        public UInt16 getUInt16()
        {
            subarrayReverseOrder(data, index, 2);
            UInt16 result = BitConverter.ToUInt16(data, index);
            index += 2;
            return result;
        }

        public UInt32 getUInt32()
        {
            subarrayReverseOrder(data, index, 4);
            UInt32 result = BitConverter.ToUInt32(data, index);
            index += 4;
            return result;
        }

        public UInt64 getUInt64()
        {
            subarrayReverseOrder(data, index, 8);
            UInt64 result = BitConverter.ToUInt64(data, index);
            index += 8;
            return result;
        }

        public Single getSingle()
        {
            subarrayReverseOrder(data, index, 4);
            Single result = BitConverter.ToSingle(data, index);
            index += 4;
            return result;
        }


        /**
            Reverses the byte order if the system is LittleEndian
            Should ONLY be used once on the same byte array, as it alters the array.
        */
        public static void subarrayReverseOrder(byte[] original, int offset, int length)
        {
            if (System.BitConverter.IsLittleEndian)
                System.Array.Reverse(original, offset, length);
        }
    }
}
