﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using VrMMOServer;
using NUnit.Framework;

namespace VrMMOServerValidation
{
    [TestFixture()]
    class MovingTest : GamePacketListener
    {
        public Stopwatch sw = Stopwatch.StartNew();

        [TestCase()]
        public void outwardMovingTest()
        {
            Console.WriteLine("Starting server");
            startServer();
            fakeListeningClient();
            fakeForwardMovingClient(1000, 10);
            Thread.Sleep(5000);
            shutdownServer();
            startServer();
        }

        public float gamedeg2rad(float deg)
        {
            return (float)((Math.PI / 2.0 - deg * 2.0 * Math.PI / 360.0) % (2.0 * Math.PI));
        }

        public void runScaleTest()
        {
            int numberFakeClients = 20;
            Console.WriteLine("Starting server");
            startServer();

            while (true)
            {
                for (int x = 0; x < numberFakeClients; x++)
                {
                    fakeRandomMovingClient(15000, 20);
                }

                Thread.Sleep(20000);
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
            float YdistancePerIteration = (float)Math.Sin(gamedeg2rad(direction)) * distance / iterationsTotal;
            float XdistancePerIteration = (float)Math.Cos(gamedeg2rad(direction)) * distance / iterationsTotal;
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
            fakeMovingClient(time, distance, (float)(r.NextDouble() * 360));
        }
    }
}
