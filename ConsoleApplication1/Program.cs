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
            //new Program().run();
            new Program().runScaleTest();
        }

        public Stopwatch sw = Stopwatch.StartNew();
        public void run()
        {
            Console.WriteLine("Starting server");
            startServer();
            fakeListeningClient();

            while (true)
            {
                fakeForwardMovingClient(10000, 5);

                Thread.Sleep(20000);
                shutdownServer();
                startServer();
            }

        }

        public void runScaleTest()
        {
            int numberFakeClients = 5;
            Console.WriteLine("Starting server");
            startServer();

            while (true)
            {
                for (int x = 0; x < numberFakeClients; x++)
                {
                    fakeRandomMovingClient(50000, 50);
                }

                Thread.Sleep(50000);
                shutdownServer();
                startServer();
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
                    Thread.Sleep(1000/30);
                    gnc2.sendUpdatePlayer(new EntityUpdatePacket());
                }
            }).Start();
        }



        public void fakeMovingClient(UInt32 time, float distance, float direction)
        {
            UInt32 clientPacketsPerSecond = 30;
            UInt32 waitBetweenPackets = 1000 / clientPacketsPerSecond;
            UInt32 iterationsTotal = time / waitBetweenPackets;
            float YdistancePerIteration = (float)Math.Sin(direction) * distance / iterationsTotal;
            float XdistancePerIteration = (float)Math.Cos(direction) * distance / iterationsTotal;
            GameNetworkingClient gnc;
            gnc = getGNP();
            gnc.startClient();
            float ypos = 0;
            float xpos = 0;
            new Thread(() =>
            {
                for (int i = 0; i < iterationsTotal; i++)
                {
                    Thread.Sleep((int)waitBetweenPackets);
                    EntityUpdatePacket eup = new EntityUpdatePacket();
                    ypos += YdistancePerIteration;
                    xpos += XdistancePerIteration;
                    eup.y = ypos;
                    eup.x = xpos;
                    eup.leftRight = direction;
                    gnc.sendUpdatePlayer(eup);
                }
                gnc.shutdown();
            }).Start();
        }

        public void fakeForwardMovingClient(UInt32 time, float distance)
        {
            fakeMovingClient(time, distance, 0f);
        }

        public void fakeRandomMovingClient(UInt32 time, float distance)
        {
            Random r = new Random();
            fakeMovingClient(time, distance, (float)(r.NextDouble() * Math.PI * 2));
        }
    }
}
