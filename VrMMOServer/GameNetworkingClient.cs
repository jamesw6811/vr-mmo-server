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
        public const string ipstring = "127.0.0.1";
        private GamePacketCoordinator coordinator;
        private UdpClient udpClient;
        private int port = 33333;
        private IPEndPoint e;
        private GamePacketListener gamePacketListener = null;

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
            udpClient = new UdpClient(ipstring, port);
            udpClient.BeginReceive(new AsyncCallback(recv), null);
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
            Byte[] receiveBytes = udpClient.EndReceive(ar, ref e);
            udpClient.BeginReceive(new AsyncCallback(recv), null);

            GamePacket gp = coordinator.parseIncomingPacket(receiveBytes);
            notifyPacketListeners(gp);

            lock (Program.consoleLock)
            {
                Console.WriteLine("Client Received: " + receiveBytes.Length.ToString());
                Console.WriteLine(gp.GetType());
                Console.WriteLine(coordinator.getAckStatusString());
            }
        }

        public void sendUpdatePlayer(EntityUpdatePacket eup)
        {
            coordinator.sendPacketToServer(udpClient, eup);
        }

        public void startTestSequence()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                Console.WriteLine("Client testing thread started");
                for (int x = 0; x < 10; x++)
                {
                    EntityUpdatePacket eup = new EntityUpdatePacket();
                    eup.x = x;
                    eup.y = x;
                    sendUpdatePlayer(eup);
                    Thread.Sleep(1000 / 30);
                }
            }).Start();
        }
    }

    public interface GamePacketListener
    {
        void receiveGamePacket(GamePacket gp);
    }
}
