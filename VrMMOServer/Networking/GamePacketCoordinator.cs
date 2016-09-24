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
        private const int num_received_packets_per_ping = 5;
        private static Int64 packets_sent = 0;
        private static Int64 time_start = GameServer.getServerStopwatchMillis();
        private Queue<GamePacket> gamePacketSendQueue;
        private Queue<ReceivedDataPacket> receivedPacketQueue;
        private ReliablePacketQueue reliablePacketQueue;

        private UInt16 next_sequence_to_send = 0;
        private UInt32 our_ack_bitfield;
        private UInt16 our_ack_sequence_id;

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
            our_ack_sequence_id = 0;
            their_ack_sequence_id = 0;
            their_ack_bitfield = 0;
            readyForDisconnect = false;
            timeLastPacketReceived = Int64.MaxValue;
            gamePacketSendQueue = new Queue<GamePacket>();
            receivedPacketQueue = new Queue<ReceivedDataPacket>();
            reliablePacketQueue = new ReliablePacketQueue();
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

        protected GamePacket parseIncomingPacket(ReceivedDataPacket dp)
        {
            Byte[] data = dp.data;
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

        public void queueIncomingPacketData(ReceivedDataPacket rdp)
        {
            lock (this)
            {
                receivedPacketQueue.Enqueue(rdp);
            }
        }

        public void addPacketToSendQueue(GamePacket gp)
        {
            lock (this)
            {
                gamePacketSendQueue.Enqueue(gp);
            }
        }

        /// <summary>
        /// Sends and parses data queued from and to client. This method is done in one action
        /// so that packets can be effectively packed and encoded with acknowledgement bits.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public List<GamePacket> sendAndReceiveQueuedPackets(UdpClient client)
        {
            List<GamePacket> receivedPackets = new List<GamePacket>();
            lock (this)
            {
                queueReliableResends();

                bool receivedPacketsHasMore = receivedPacketQueue.Count > 0;
                bool sendingPacketsHasMore = gamePacketSendQueue.Count > 0;
                int receivedPacketsWithoutResponse = 0;

                // Receive packets and send packets interlaced until there are no more to send/receive.
                while (receivedPacketsHasMore || sendingPacketsHasMore)
                {
                    if (receivedPacketsHasMore)
                    {
                        GamePacket nextReceived = parseIncomingPacket(receivedPacketQueue.Dequeue());
                        receivedPackets.Add(nextReceived);
                    }

                    if (sendingPacketsHasMore)
                    {
                        GamePacket nextToSend = gamePacketSendQueue.Dequeue();
                        sendPacket(client, nextToSend);
                    }
                    else
                    {
                        // Send a ping packet if there are more received packets than sent packets so that acknowledgements are made.
                        receivedPacketsWithoutResponse++;
                        if (receivedPacketsWithoutResponse >= num_received_packets_per_ping)
                        {
                            receivedPacketsWithoutResponse = 0;
                            sendPacket(client, new PingPacket());
                        }
                    }

                    receivedPacketsHasMore = receivedPacketQueue.Count > 0;
                    sendingPacketsHasMore = gamePacketSendQueue.Count > 0;
                }
            }

            return receivedPackets;
        }

        private void queueReliableResends()
        {
            lock (this)
            {
                List<GamePacket> resends = reliablePacketQueue.getResendPacketList(GameServer.getServerStopwatchMillis());
                foreach (GamePacket gp in resends)
                {
                    gamePacketSendQueue.Enqueue(gp);
                }
            }
        }

        protected void sendPacket(UdpClient client, GamePacket gp)
        {
            IPEndPoint ep = null;
            if (onlinePlayerEntity != null)
            {
                ep = onlinePlayerEntity.ip;
            }
            if (gp.reliable)
            {
                reliablePacketQueue.addPacket(gp, next_sequence_to_send, GameServer.getServerStopwatchMillis());
            }

            NetworkDataWriter ndw = new NetworkDataWriter();
            writeNextPacketHeader(ndw);
            next_sequence_to_send++;
            gp.write(ndw);
            byte[] bytes = ndw.getByteArray();

            try
            {
                client.BeginSend(bytes, bytes.Length, ep, new AsyncCallback(sentPacket), null);
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
            ndw.writeUInt16(our_ack_sequence_id);
            ndw.writeUInt32(our_ack_bitfield);
        }

        public string getAckStatusString()
        {
            string status = "";
            status += "Receiving: " + our_ack_sequence_id.ToString() + " " + Convert.ToString(our_ack_bitfield, 2) + "\r\n";
            status += "Sending: " + their_ack_sequence_id.ToString() + " " + Convert.ToString(their_ack_bitfield, 2) + "\r\n";
            return status;
        }

        /**
            Flag the packet received from the other party as received for future acknowledgement
        */
        private void flagReceivedPacketAck(UInt16 new_sequence_id)
        {
            if (isMoreRecentSequence(new_sequence_id, our_ack_sequence_id))
            {
                int sequence_separation = getSequenceSeparation(new_sequence_id, our_ack_sequence_id);
                our_ack_bitfield = our_ack_bitfield << sequence_separation;
                our_ack_sequence_id = new_sequence_id;
                our_ack_bitfield = our_ack_bitfield | ((UInt32)1 << sequence_separation - 1);
            }
            else
            {
                int sequence_separation = getSequenceSeparation(our_ack_sequence_id, new_sequence_id);
                if (sequence_separation <= max_bitfield && sequence_separation > 0)
                {
                    our_ack_bitfield = our_ack_bitfield | ((UInt32)1 << sequence_separation - 1);
                }
            }
        }

        /**
            Update the received packet acknowledgement bitfield and id for the other party
        */
        private void updateSentPacketAck(UInt16 new_ack_sequence_id, UInt32 new_ack_bitfield)
        {
            // Make sure to only use latest ack field, not out of date
            if (isMoreRecentSequence(new_ack_sequence_id, their_ack_sequence_id))
            {
                // Diff ack fields to determine newly received packets
                int sequence_separation = getSequenceSeparation(new_ack_sequence_id, their_ack_sequence_id);
                UInt32 diff_bitfield = their_ack_bitfield;
                diff_bitfield = (diff_bitfield << 1) | 1; // Add last sequence id
                diff_bitfield = diff_bitfield << (sequence_separation - 1); // Correct frame
                diff_bitfield = diff_bitfield ^ new_ack_bitfield; // Filter only newly flipped bits
                reliablePacketQueue.acknowledgePacket(new_ack_sequence_id); // Notify current acknowledgement
                for (int x = new_ack_sequence_id - 1; x > new_ack_sequence_id - max_bitfield - 1; x--) // Notify other new packets
                {
                    if ((diff_bitfield & 1) > 0)
                    {
                        UInt16 sequence_number = (UInt16)(x % (max_sequence + 1));
                        reliablePacketQueue.acknowledgePacket(sequence_number);
                    }
                    diff_bitfield = diff_bitfield >> 1;
                }

                // Update ack fields
                their_ack_bitfield = new_ack_bitfield;
                their_ack_sequence_id = new_ack_sequence_id;
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

    class ReceivedDataPacket
    {
        public IPEndPoint endpoint;
        public Byte[] data;

        public ReceivedDataPacket(Byte[] d, IPEndPoint e)
        {
            endpoint = e;
            data = d;
        }
    }
}
