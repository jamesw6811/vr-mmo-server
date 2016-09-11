using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;

namespace VrMMOServer
{
    class Program
    {
        public static Object consoleLock = new Object();
        static void Main(string[] args)
        {
            Console.WriteLine("Starting game server...");
            GameServer server = new GameServer();
            server.startServer();
            //runClientTests();
            //runClientTests();
            Console.WriteLine("Game server listening...");
            Console.ReadKey();
            server.shutdown();
            Console.ReadKey();
        }

        static void runClientTests()
        {
            GameNetworkingClient gc = new GameNetworkingClient();
            gc.startClient();
            gc.startTestSequence();
        }
    }

    class GameServer
    {
        private Dictionary<IPEndPoint, GamePacketCoordinator> coordinators = new Dictionary<IPEndPoint, GamePacketCoordinator>();
        private UdpClient udpServer;
        private const int port = 33333;
        private IPEndPoint e;
        private GameWorld world;
        private bool running = true;
        private Int64 lastUpdateTime = 0;

        public void startServer()
        {
            world = new GameWorld();
            e = new IPEndPoint(IPAddress.Any, port);
            udpServer = new UdpClient(port);
            udpServer.BeginReceive(new AsyncCallback(recv), null);
            beginUpdates();
        }

        public void shutdown()
        {
            running = false;
        }

        private void recv(IAsyncResult ar)
        {
            // Receive packet from UDP server
            Byte[] receiveBytes = udpServer.EndReceive(ar, ref e);
            udpServer.BeginReceive(new AsyncCallback(recv), null);

            // Find or create packet coordinator
            GamePacketCoordinator gpc;
            if (!coordinators.TryGetValue(e, out gpc))
            {
                OnlinePlayerEntity ope = new OnlinePlayerEntity();
                ope.ip = e;
                world.addEntity(ope);
                gpc = new GamePacketCoordinator();
                gpc.onlinePlayerEntity = ope;
                coordinators.Add(e, gpc);
                lock (Program.consoleLock)
                {
                    Console.WriteLine("Added player");
                }
            }

            // Receive and parse Game Packet
            GamePacket gp = gpc.parseIncomingPacket(receiveBytes);

            handleGamePacket(gp, gpc.onlinePlayerEntity);

            lock (Program.consoleLock)
            {
                Console.WriteLine("Server Received: " + receiveBytes.Length.ToString());
                Console.WriteLine(gp.GetType());
                Console.WriteLine(gpc.getAckStatusString());
            }
        }

        /// <summary>
        /// Handle GamePacket gp, received from OnlinePlayerEntity ope
        /// </summary>
        /// <param name="gp"></param>
        /// <param name="ope"></param>
        private void handleGamePacket(GamePacket gp, OnlinePlayerEntity ope)
        {
            if (gp is EntityUpdatePacket)
            {
                EntityUpdatePacket eup = (EntityUpdatePacket)gp;
                eup.id = ope.id;
                world.updateEntity(eup);
            }
        }

        /**
            Begin threaded update loop to send updates to all connected entities
        */
        private void beginUpdates()
        {
            new Thread(() =>
            {
                Stopwatch stopWatch = new Stopwatch();
                while (running)
                {
                    stopWatch.Reset();
                    stopWatch.Start();
                    runUpdateLoop();
                    while (stopWatch.ElapsedMilliseconds < 1000/30)
                    {
                        Thread.Sleep(1);
                    }
                    lock (Program.consoleLock)
                    {
                        Console.WriteLine("Elapsed update time: " + stopWatch.ElapsedMilliseconds);
                    }
                }
            }).Start();
        }

        private void runUpdateLoop()
        {
            // For each game entity, send updates to each connection
            foreach (GameEntity ge in world.getAllEntities())
            {
                EntityUpdatePacket eup = EntityUpdatePacket.fromEntity(ge);
                foreach (GamePacketCoordinator gpc in coordinators.Values)
                {
                    // If the game entity isn't bound to this connection, send an update.
                    if (!gpc.boundToEntity(ge))
                    {
                        gpc.sendPacketToClient(udpServer, eup);
                    }
                }
            }
        }
       
    }

}
