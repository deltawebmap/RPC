using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LibDeltaSystem;
using LibDeltaSystem.RPC;
using LibDeltaSystem.Tools.InternalComms;
using Newtonsoft.Json;
using static LibDeltaSystem.Tools.InternalComms.InternalCommsServer;

namespace DeltaUserGateway.Sender
{
    public class SenderConnectionV2 : InternalCommsServerClient
    {
        public SenderConnectionV2(DeltaConnection conn, byte[] key, Socket sock, InternalCommsServer server) : base(conn, key, sock, server)
        {
            
        }

        public override async Task HandleMessage(int opcode, Dictionary<string, byte[]> payloads)
        {
            switch(opcode)
            {
                case 1: await SendGatewayMsg(payloads, RPCType.RPCSession); break;
                case 2: await SendGatewayMsg(payloads, RPCType.RPCNotifications); break;
            }
        }

        private async Task SendGatewayMsg(Dictionary<string, byte[]> payloads, RPCType type)
        {
            //Decode the first chunk as a filter
            RPCFilter filter = JsonConvert.DeserializeObject<RPCFilter>(Encoding.UTF8.GetString(payloads["FILTER"]));

            //Distribute messages
            await SessionHolder.DistributeMessage(filter, payloads["DATA"], type);
        }
    }
}
