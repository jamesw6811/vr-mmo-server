using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VrMMOServer
{
    class ReliablePacketQueue
    {
        private Dictionary<UInt16, ReliableGamePacket> sequenceToPacket;
        private Queue<ReliableGamePacket> resendQueue;
        private const long TIME_OUT_RTT_RESEND = 2000;

        public ReliablePacketQueue()
        {
            sequenceToPacket = new Dictionary<ushort, ReliableGamePacket>();
            resendQueue = new Queue<ReliableGamePacket>();
        }

        public void addPacket(GamePacket gp, UInt16 sequence, long time)
        {
            ReliableGamePacket rgp = new ReliableGamePacket(gp, time + TIME_OUT_RTT_RESEND);
            sequenceToPacket.Add(sequence, rgp);
            resendQueue.Enqueue(rgp);
        }

        public void acknowledgePacket(UInt16 sequence)
        {
            ReliableGamePacket rgp = null;
            sequenceToPacket.TryGetValue(sequence, out rgp);
            if (rgp != null)
            {
                Console.WriteLine("Acknowledged sent packet: " + rgp.gp.ToString());
                rgp.acknowledged = true;
                sequenceToPacket.Remove(sequence);
            }
        }

        public List<GamePacket> getResendPacketList(long time)
        {
            List<GamePacket> resendList = new List<GamePacket>();
            while (resendQueue.Count > 0)
            {
                ReliableGamePacket rgp = resendQueue.Peek();
                if (time >= rgp.timeOutTime)
                {
                    resendQueue.Dequeue();
                    if (!rgp.acknowledged)
                    {
                        Console.WriteLine("Resending unacknowledged packet: " + rgp.gp.ToString());
                        resendList.Add(rgp.gp);
                    }
                } 
                else
                {
                    break;
                }
            }
            return resendList;
        }
    }

    class ReliableGamePacket
    {
        public GamePacket gp;
        public long timeOutTime;
        public Boolean acknowledged;

        public ReliableGamePacket(GamePacket g, long tot)
        {
            gp = g;
            timeOutTime = tot;
            acknowledged = false;
        }
    }
}
