using LibDeltaSystem;
using LibDeltaSystem.Tools.InternalComms;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace DeltaUserGateway.Sender
{
    public class SenderServerV2 : InternalCommsServer
    {
        public SenderServerV2(DeltaConnection conn, byte[] key, int port) : base(conn, key, port)
        {

        }

        public override InternalCommsServerClient GetClient(DeltaConnection conn, byte[] key, Socket sock)
        {
            return new SenderConnectionV2(conn, key, sock, this);
        }

        public override void OnClientAuthorized(InternalCommsServerClient client)
        {
            
        }

        public override void OnClientDisconnected(InternalCommsServerClient client)
        {
            
        }
    }
}
