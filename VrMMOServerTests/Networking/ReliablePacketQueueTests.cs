using NUnit.Framework;
using VrMMOServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VrMMOServer.Tests
{
    [TestFixture()]
    public class ReliablePacketQueueTests
    {
        [Test()]
        public void ReliablePacketQueueTest()
        {
            ReliablePacketQueue rpq = new ReliablePacketQueue();
        }

        [Test()]
        public void addPacketTest()
        {
            ReliablePacketQueue rpq = new ReliablePacketQueue();
            GamePacket gp = new EntityRemovePacket();
            rpq.addPacket(gp, 1, 1000);
        }

        [Test()]
        public void acknowledgePacketTest()
        {
            ReliablePacketQueue rpq = new ReliablePacketQueue();
            GamePacket gp1 = new EntityRemovePacket();
            GamePacket gp2 = new EntityRemovePacket();
            long startTime = 1000;
            rpq.addPacket(gp1, 1, startTime);
            rpq.addPacket(gp2, 2, startTime);
            rpq.acknowledgePacket(1);
            List<GamePacket> gplist = rpq.getResendPacketList(startTime + ReliablePacketQueue.TIME_OUT_RTT_RESEND + 1);
            Assert.AreEqual(1, gplist.Count);
            Assert.AreSame(gp2, gplist.First());
        }

        [Test()]
        public void getResendPacketListTest()
        {
            ReliablePacketQueue rpq = new ReliablePacketQueue();
            GamePacket gp1 = new EntityRemovePacket();
            GamePacket gp2 = new EntityRemovePacket();
            long startTime = 1000;
            rpq.addPacket(gp1, 1, startTime);
            rpq.addPacket(gp2, 2, startTime);
            List<GamePacket> gplist = rpq.getResendPacketList(startTime + ReliablePacketQueue.TIME_OUT_RTT_RESEND + 1);
            Assert.AreEqual(2, gplist.Count);
        }
    }
}