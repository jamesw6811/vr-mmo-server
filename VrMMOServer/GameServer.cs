﻿using System;
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
    public class GameServerProgram
    {
        public static Object consoleLock = new Object();
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting game server...");
            GameServer server = new GameServer();
            server.startServer();
            Console.WriteLine("Game server listening...");
            while (true)
            {
                Thread.Sleep(1000);
            }
        }

    }

    public class GameServer
    {
        public const int SIO_UDP_CONNRESET = -1744830452;
        public const Int64 MILLIS_PER_UPDATE = 1000 / 30;
        public const Int64 MILLIS_PER_STATUS_UPDATE = 1000 * 5;
        private static long MILLIS_STOPWATCH_FREQUENCY_DIVIDER = Stopwatch.Frequency / 1000;
        private Dictionary<IPEndPoint, GamePacketCoordinator> coordinators;
        private UdpClient udpServer;
        private const int port = 33333;
        private GameWorld world;
        private bool running = true;

        public GameServer()
        {
            coordinators = new Dictionary<IPEndPoint, GamePacketCoordinator>();
        }

        public static long getServerStopwatchMillis()
        {
            return Stopwatch.GetTimestamp() / MILLIS_STOPWATCH_FREQUENCY_DIVIDER;
        }

        public bool hasConnectedEndpoint(IPEndPoint e)
        {
            bool hasConnected = false;
            lock (coordinators)
            {
                hasConnected = coordinators.ContainsKey(e);
            }
            return hasConnected;
        }

        public int connectedEndpointCount()
        {
            int connections = 0;
            lock (coordinators)
            {
                connections = coordinators.Count;
            }
            return connections;
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
            udpServer.Close();
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
                lock (GameServerProgram.consoleLock)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            catch (ObjectDisposedException e)
            {
                return;
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
                    gpc = new GamePacketCoordinator();
                    coordinators.Add(e, gpc);
                    lock (GameServerProgram.consoleLock)
                    {
                        Console.WriteLine("Added player:" + e.ToString());
                    }
                }
            }

            // Queue packet for later parsing
            gpc.queueIncomingPacketData(new ReceivedDataPacket(receiveBytes, e));
            
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
                lock (world)
                {
                    world.updateEntity(eup);
                }
            }
        }

        /**
            Begin threaded update loop to send updates to all connected entities
        */
        private void beginUpdates()
        {
            new Thread(() =>
            {
                Int64 nextLoopTime = getServerStopwatchMillis();
                Int64 nextStatusUpdateTime = getServerStopwatchMillis();
                while (running)
                {
                    // Loop time restriction
                    while (getServerStopwatchMillis() < nextLoopTime)
                    {
                        Thread.Sleep(1);
                    }
                    runUpdateLoop();
                    nextLoopTime += MILLIS_PER_UPDATE;

                    // Status update
                    if (getServerStopwatchMillis() > nextStatusUpdateTime)
                    {
                        nextStatusUpdateTime += MILLIS_PER_STATUS_UPDATE;
                        doStatusUpdate();
                    }

                    // Over time check
                    if (getServerStopwatchMillis() >= nextLoopTime)
                    {
                        
                    }
                }
            }).Start();
        }


        private void doStatusUpdate()
        {
            Console.WriteLine("\n\n------ Status Update ------");
            Console.WriteLine("Packets/seconds: " + GamePacketCoordinator.packetsPerSecond().ToString());
            Console.WriteLine("Packets total: " + GamePacketCoordinator.packetsTotal().ToString());
            Console.WriteLine("Time now: " + GameServer.getServerStopwatchMillis().ToString());
            Console.WriteLine("GameWorld tick: " + gameWorldTickDuration.ToString());
            Console.WriteLine("Packet Receiving: " + packetReceivingDuration.ToString());
            Console.WriteLine("Worldview Sending: " + worldViewSendingDuration.ToString());
            Console.WriteLine("Connection Updating: " + updateConnectionsDuration.ToString());
            Console.WriteLine("-----------------------------\n\n");
        }

        private Int64 packetReceivingDuration = 0;
        private Int64 worldViewSendingDuration = 0;
        private Int64 updateConnectionsDuration = 0;
        private Int64 gameWorldTickDuration = 0;
        private void runUpdateLoop()
        {
            gameWorldTickDuration = getServerStopwatchMillis();
            world.doGameWorldTick();
            gameWorldTickDuration = getServerStopwatchMillis() - gameWorldTickDuration;

            packetReceivingDuration = getServerStopwatchMillis();
            handleReceivedPacketQueue();
            packetReceivingDuration = getServerStopwatchMillis() - packetReceivingDuration;

            worldViewSendingDuration = getServerStopwatchMillis();
            updateWorldViews();
            worldViewSendingDuration = getServerStopwatchMillis() - worldViewSendingDuration;

            updateConnectionsDuration = getServerStopwatchMillis();
            updateConnections();
            updateConnectionsDuration = getServerStopwatchMillis() - updateConnectionsDuration;
        }


        /// <summary>
        /// Handle all queued received packet data
        /// </summary>
        private void handleReceivedPacketQueue()
        {            
            lock (coordinators)
            {
                foreach (KeyValuePair<IPEndPoint, GamePacketCoordinator> kvp in coordinators)
                {
                    IPEndPoint ep = kvp.Key;
                    GamePacketCoordinator gpc = kvp.Value;

                    // Handle unattached coordinator by creating new online player
                    if (gpc.onlinePlayerEntity == null)
                    {
                        createNewOnlinePlayerEntity(ep, gpc);
                    }

                    // Handle all received game packets
                    List<GamePacket> gp_list = gpc.sendAndReceiveQueuedPackets(udpServer);
                    foreach (GamePacket gp in gp_list)
                    {
                        handleGamePacket(gp, gpc.onlinePlayerEntity);
                    }
                }
            }
        }

        private void createNewOnlinePlayerEntity(IPEndPoint ep, GamePacketCoordinator gpc)
        {
            OnlinePlayerEntity opc = new OnlinePlayerEntity();
            opc.ip = ep;
            world.addEntity(opc);
            gpc.onlinePlayerEntity = opc;
        }

        /// <summary>
        /// Send an idling packet to each connection if no other packets have been sent.
        /// </summary>
        private void sendPingPacket(GamePacketCoordinator gpc, Int64 pingThreshold)
        {
            if (gpc.timeLastPacketSent < pingThreshold)
            {
                PingPacket pp = new PingPacket();
                gpc.addPacketToSendQueue(pp);
            }
        }


        /// <summary>
        /// Update view of the world for all connected players that have world entities.
        /// </summary>
        private void updateWorldViews()
        {
            // For any removed entities, send removal notifications
            foreach (GameEntity ge in world.pullRemovedEntities())
            {
                lock (coordinators)
                {
                    EntityRemovePacket erp = EntityRemovePacket.fromEntity(ge);
                    foreach (GamePacketCoordinator gpc in coordinators.Values)
                    {
                        if (gpc.onlinePlayerEntity != null)
                        {
                            gpc.addPacketToSendQueue(erp);
                        }
                    }
                }
            }

            // For each game entity, send updates to each connection
            foreach (GameEntity ge in world.getAllEntities())
            {
                lock (coordinators)
                {
                    EntityUpdatePacket eup = EntityUpdatePacket.fromEntity(ge);
                    foreach (GamePacketCoordinator gpc in coordinators.Values)
                    {
                        // If the game entity isn't bound to this connection, send an update.
                        if (gpc.onlinePlayerEntity != null && !gpc.boundToEntity(ge))
                        {
                            gpc.addPacketToSendQueue(eup);
                        }
                    }
                }
            }
        }

        private void updateConnections()
        {
            Int64 pingThreshold = getServerStopwatchMillis() - MILLIS_PER_UPDATE * 2;
            Int64 disconnectThreshold = getServerStopwatchMillis() - 3000;
            List<IPEndPoint> ipsToRemove = new List<IPEndPoint>();
            lock (coordinators)
            {
                foreach (GamePacketCoordinator gpc in coordinators.Values)
                {
                    sendPingPacket(gpc, pingThreshold);
                    updateConnectionStatus(gpc, disconnectThreshold);
                    if (gpc.isReadyForDisconnect())
                    {
                        ipsToRemove.Add(gpc.onlinePlayerEntity.ip);
                    }
                }
                foreach (IPEndPoint e in ipsToRemove)
                {
                    coordinators.Remove(e);
                }
            }
        }

        /// <summary>
        /// Update each connections status based on last packet times
        /// </summary>
        private void updateConnectionStatus(GamePacketCoordinator gpc, Int64 disconnectThreshold)
        {
            if (gpc.timeLastPacketReceived < disconnectThreshold)
            {
                gpc.setReadyForDisconnect();
                Console.WriteLine("Player left:" + gpc.onlinePlayerEntity.ip.ToString());
            }
            if (gpc.isReadyForDisconnect())
            {
                lock (world)
                {
                    world.serverRemoveEntity(gpc.onlinePlayerEntity.id);
                }
            }
        }
    }




}
