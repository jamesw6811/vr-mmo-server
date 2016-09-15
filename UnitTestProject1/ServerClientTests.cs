using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VrMMOServer;
using System.Threading;

namespace VrMMOServerTests
{
    [TestClass]
    public class ServerClientTests
    {
        [TestMethod]
        public void TestServerSingleClient()
        {
            GameServer gs = new GameServer();
            gs.startServer();
            Assert.AreEqual(0, gs.connectedEndpointCount());
            GameNetworkingClient gnc = new GameNetworkingClient("127.0.0.1");
            gnc.startClient();
            Thread.Sleep(2000);
            Assert.AreEqual(1, gs.connectedEndpointCount());
            gs.shutdown();
            gnc.shutdown();
        }

        [TestMethod]
        public void TestServerClientDisconnect()
        {
            GameServer gs = new GameServer();
            gs.startServer();
            Assert.AreEqual(0, gs.connectedEndpointCount());
            GameNetworkingClient gnc = new GameNetworkingClient("127.0.0.1");
            gnc.startClient();
            Thread.Sleep(2000);
            Assert.AreEqual(1, gs.connectedEndpointCount());
            gnc.shutdown();
            Thread.Sleep(4000);
            Assert.AreEqual(0, gs.connectedEndpointCount());
            gs.shutdown();
        }

        [TestMethod]
        public void TestServerManyClients()
        {
            GameNetworkingClient gnc;
            for (int x = 0; x < 3; x++)
            {
                gnc = new GameNetworkingClient();
                gnc.startClient();
                new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(20);
                        gnc.sendUpdatePlayer(new EntityUpdatePacket());
                    }
                }).Start();
                Thread.Sleep(100);
            }
            Thread.Sleep(200000);
        }
    }
}
