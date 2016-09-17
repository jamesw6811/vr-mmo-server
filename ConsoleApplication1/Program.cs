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
            startServer();

            GameNetworkingClient gnc;
            for (int x = 0; x < 1; x++)
            {
                gnc = getGNP();
                gnc.startClient();
                new Thread(() =>
                {
                    for (int y = 0; y < 50; y++)
                    {
                        Thread.Sleep(20);
                        gnc.sendUpdatePlayer(new EntityUpdatePacket());
                    }
                    gnc.shutdown();
                }).Start();
                Thread.Sleep(100);
            }

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
            Thread.Sleep(6000);

            Debug.Assert(removePacketsReceived == 1);

            Console.ReadLine();
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

        public void startServer()
        {
            GameServer gs = new GameServer();
            gs.startServer();
        }
    }
}
