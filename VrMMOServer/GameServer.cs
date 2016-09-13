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
            while (true)
            {
                Thread.Sleep(1000);
            }
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
        public const int SIO_UDP_CONNRESET = -1744830452;
        private Dictionary<IPEndPoint, GamePacketCoordinator> coordinators;
        private UdpClient udpServer;
        private const int port = 33333;
        private GameWorld world;
        private bool running = true;
        private Int64 lastUpdateTime = 0;

        public GameServer()
        {
            coordinators = new Dictionary<IPEndPoint, GamePacketCoordinator>();
        }

        public void startServer()
        {
            world = new GameWorld();
            startUDPServer();
            beginUpdates();
        }

        private void startUDPServer()
        {
            udpServer = new UdpClient(port);
            udpServer.BeginReceive(new AsyncCallback(recv), null);
	    try
	    {
            	// Magic away the silly disconnection logic in UdpClient
            	udpServer.Client.IOControl(
                	(IOControlCode)SIO_UDP_CONNRESET,
                	new byte[] { 0, 0, 0, 0 },
                	null
            	);
	    }
	    catch
	    {
		Console.WriteLine("IOControl threw exception, SIO_UDP_CONNRESET option not set");
	    }
        }

        public void shutdown()
        {
            running = false;
        }

        private void recv(IAsyncResult ar)
        {
            try
            {
                // Receive packet from UDP server
                IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);
                Byte[] receiveBytes = udpServer.EndReceive(ar, ref ep);
                udpServer.BeginReceive(new AsyncCallback(recv), null);
                handleReceivedPacketData(receiveBytes, ep);
            }
            catch (SocketException e)
            {
                udpServer.BeginReceive(new AsyncCallback(recv), null);
                lock (Program.consoleLock)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        private void handleReceivedPacketData(Byte[] receiveBytes, IPEndPoint e)
        {
            // Find or create packet coordinator
            GamePacketCoordinator gpc;
            lock (coordinators)
            {
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
                        Console.WriteLine("Added player:" + e.ToString());
                    }
                }
            }

            // Receive and parse Game Packet
            GamePacket gp = gpc.parseIncomingPacket(receiveBytes);

            handleGamePacket(gp, gpc.onlinePlayerEntity);

            /*
            lock (Program.consoleLock)
            {
                Console.WriteLine("Server Received: " + receiveBytes.Length.ToString());
                Console.WriteLine(gp.GetType());
                Console.WriteLine(gpc.getAckStatusString());
            }
            */
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
                        //Console.WriteLine("Elapsed update time: " + stopWatch.ElapsedMilliseconds);
                    }
                }
            }).Start();
        }

        private void runUpdateLoop()
        {
            // For each game entity, send updates to each connection
            foreach (GameEntity ge in world.getAllEntities())
            {
                lock (coordinators)
                {
                    EntityUpdatePacket eup = EntityUpdatePacket.fromEntity(ge);
                    foreach (GamePacketCoordinator gpc in coordinators.Values)
                    {
                        // If the game entity isn't bound to this connection, send an update.
                        if (!gpc.boundToEntity(ge))
                        {
                            gpc.sendPacketToClient(udpServer, eup);
                            /*
                            lock (Program.consoleLock)
                            {
                                Console.WriteLine("Sent packet: " + gpc.onlinePlayerEntity.ip.ToString());
                            }
                            */
                        }
                    }
                }
            }
        }
       
    }

}
