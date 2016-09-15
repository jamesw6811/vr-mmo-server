using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace VrMMOServer
{

    public class GameNetworkingClient
    {
        public string ipstring = "104.196.10.213";
        public const Int64 MILLIS_PER_UPDATE = 1000 / 30;
        private bool running;
        private GamePacketCoordinator coordinator;
        private UdpClient udpClient;
        private int port = 33333;
        private IPEndPoint e;
        private GamePacketListener gamePacketListener = null;

        public GameNetworkingClient()
        {
            initialize();
        }

        public GameNetworkingClient(string customip)
        {
            ipstring = customip;
            initialize();
        }

        private void initialize()
        {
            running = true;
        }

        public void registerPacketListener(GamePacketListener gpl)
        {
            gamePacketListener = gpl;
        }

        public void startClient()
        {
            coordinator = new GamePacketCoordinator();

            IPAddress address;
            IPAddress.TryParse(ipstring, out address);
            e = new IPEndPoint(address, port);
            udpClient = new UdpClient();
            udpClient.Connect(e);
            udpClient.BeginReceive(new AsyncCallback(recv), null);

            beginUpdateLoop(); // Send ping packets when idle, etc.
        }

        public void shutdown()
        {
            running = false;
            udpClient.Close();
        }

        private void beginUpdateLoop()
        {
            new Thread(() =>
            {
                Int64 nextLoopTime = GameServer.getServerStopwatchMillis();
                while (running)
                {
                    while (GameServer.getServerStopwatchMillis() < nextLoopTime)
                    {
                        Thread.SpinWait(1);
                    }
                    runUpdateLoop();
                    nextLoopTime += MILLIS_PER_UPDATE;
                    if (GameServer.getServerStopwatchMillis() >= nextLoopTime)
                    {
                        // TODO handle falling behind
                    }
                }
            }).Start();
        }

        private void runUpdateLoop()
        {
            if (GameServer.getServerStopwatchMillis() - coordinator.timeLastPacketSent > MILLIS_PER_UPDATE*2)
            {
                PingPacket pp = new PingPacket();
                coordinator.sendPacketToServer(udpClient, pp);
            }
        }

        private void notifyPacketListeners(GamePacket gp)
        {
            if(gamePacketListener != null)
            {
                gamePacketListener.receiveGamePacket(gp);
            }
        }

        private void recv(IAsyncResult ar)
        {
            if(gamePacketListener!= null)
            {
                Console.WriteLine("recv");
            }
            try
            {
                Byte[] receiveBytes = udpClient.EndReceive(ar, ref e);

                udpClient.BeginReceive(new AsyncCallback(recv), null);

                GamePacket gp = coordinator.parseIncomingPacket(receiveBytes);
                notifyPacketListeners(gp);
            }
            catch (SocketException e)
            {
            }
            catch (ObjectDisposedException e)
            {
                return;
            }

        }

        public void sendUpdatePlayer(EntityUpdatePacket eup)
        {
            coordinator.sendPacketToServer(udpClient, eup);
        }
        
    }

    public interface GamePacketListener
    {
        void receiveGamePacket(GamePacket gp);
    }
}
