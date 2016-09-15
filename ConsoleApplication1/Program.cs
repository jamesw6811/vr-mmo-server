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
            GameNetworkingClient gnc;
            for (int x = 0; x < 1; x++)
            {
                gnc = getGNP();
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
            gnc = getGNP();
            gnc.startClient();
            gnc.registerPacketListener(this);
            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(20);
                    gnc.sendUpdatePlayer(new EntityUpdatePacket());
                }
            }).Start();
            Thread.Sleep(100);

            Console.ReadLine();
        }

        public GameNetworkingClient getGNP()
        {
            return new GameNetworkingClient();
        }

        public long packetsReceived = 0;
        public void receiveGamePacket(GamePacket gp)
        {
            Console.WriteLine(packetsReceived++);
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}
