using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DeltaUserGateway.Sender
{
    public static class SenderServer
    {
        public static Socket server;
        public static byte[] key;

        public static void StartServer(byte[] rpc_key, int port)
        {
            //Start server
            key = rpc_key;
            server = new Socket(SocketType.Stream, ProtocolType.Tcp);
            server.Bind(new IPEndPoint(IPAddress.Any, port));
            server.Listen(32);
            server.BeginAccept(OnAcceptConnection, null);
        }

        private static void OnAcceptConnection(IAsyncResult r)
        {
            //Get state
            Socket sock = server.EndAccept(r);

            //Create new connection
            SenderConnection conn = new SenderConnection(sock);

            //Set up
            conn.BeginReceive();

            //Send salt
            sock.Send(conn.salt);

            //Accept next
            server.BeginAccept(OnAcceptConnection, null);
        }
    }
}
