using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using VrMMOServer;

namespace ConsoleApplication1
{
    class Program : GamePacketListener
    {
        static void Main(string[] args)
        {
            new Program().run();
        }

        public Stopwatch sw = Stopwatch.StartNew();
        public void run()
        {
            Console.WriteLine("Starting server");

            while (true)
            {
                startServer();
                fakeForwardMovingClient(10000, 5);
                fakeListeningClient();

                Thread.Sleep(20000);
                shutdownServer();
            }

        }

        public GameNetworkingClient getGNP()
        {
            return new GameNetworkingClient("127.0.0.1");
        }

        public long packetsReceived = 0;
        public long removePacketsReceived = 0;
        public void receiveGamePacket(GamePacket gp)
        {
            if (gp is EntityRemovePacket)
            {
                Console.WriteLine("Remove packets: " + ++removePacketsReceived);
            }
        }

        GameServer gs;
        public void startServer()
        {
            gs = new GameServer();
            gs.startServer();
        }

        public void shutdownServer()
        {
            gs.shutdown();
        }

        public void fakeListeningClient()
        {
            GameNetworkingClient gnc2;
            gnc2 = getGNP();
            gnc2.startClient();
            gnc2.registerPacketListener(this);
            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(20);
                    gnc2.sendUpdatePlayer(new EntityUpdatePacket());
                }
            }).Start();
        }

        public void fakeForwardMovingClient(UInt32 time, float distance)
        {
            GameNetworkingClient gnc;
            gnc = getGNP();
            gnc.startClient();
            float ypos = 0;
            new Thread(() =>
            {
                for (int i = 0; i < time/20; i++)
                {
                    Thread.Sleep(20);
                    EntityUpdatePacket eup = new EntityUpdatePacket();
                    ypos += distance / (time / 20);
                    eup.y = ypos;
                    gnc.sendUpdatePlayer(eup);
                }
                gnc.shutdown();
            }).Start();
        }
    }
}
