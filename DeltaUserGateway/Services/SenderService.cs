using LibDeltaSystem;
using LibDeltaSystem.Tools;
using LibDeltaSystem.WebFramework.WebSockets;
using LibDeltaSystem.WebFramework.WebSockets.Entities;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace DeltaUserGateway.Services
{
    public class SenderService : DeltaWebSocketService
    {
        public bool authenticated = false;
        public string program_identity = ""; //8 character identifier for the source
        public int version = 0;
        
        public SenderService(DeltaConnection conn, HttpContext e) : base(conn, e)
        {
        }

        public override async Task<bool> OnPreRequest()
        {
            return true;
        }

        private void Log(string topic, string msg)
        {
            Console.WriteLine($"[{topic}] {msg}");
        }

        public override async Task OnReceiveBinary(byte[] data, int length)
        {
            //If we're not authenticated, handle that first.
            Log("RECEIVE", "GOT MESSAGE of length " + length);
            if(!authenticated)
            {
                await HandleAuthentication(data, length);
                return;
            }

            //Read the opcode and index
            ushort opcode = BinaryTool.ReadUInt16(data, 0);
            ulong index = BinaryTool.ReadUInt64(data, 2);

            //Switch on this opcode
            Log("RECEIVE", "Got message with opcode " + opcode + " / index " + index);
            if (opcode == 1)
                await HandleRPCMessage(data, length, index);
            else
                await WriteResponse(index, 404);
        }

        public override async Task OnReceiveText(string data)
        {
            //Text should never be recieived!
            await DisconnectAsync(WebSocketCloseStatus.InvalidPayloadData, "DELTA_RPC_DISCONNECT_NO_TEXT_ALLOWED");
        }

        public override async Task OnSockClosed(WebSocket sock)
        {
            
        }

        public override async Task OnSockOpened(WebSocket sock)
        {
            
        }

        public override async Task<bool> SetArgs(Dictionary<string, string> args)
        {
            return true;
        }

        /// <summary>
        /// Handles the first binary message we get. It will always be for authentication.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private async Task HandleAuthentication(byte[] data, int length)
        {
            //This message follows the following format:
            //  256 bytes: Authentication key
            //  8 bytes: Program identification name
            //  4 bytes: System version

            //Verify
            if(length != 256 + 8 + 4)
            {
                Log("AUTHENTICATION", "Authentication length mismatch!");
                await DisconnectAsync(WebSocketCloseStatus.InvalidPayloadData, "DELTA_RPC_DISCONNECT_INVALID_AUTHENTICATION_PAYLOAD");
                return;
            }

            //Extract info
            byte[] key = BinaryTool.CopyFromArray(data, 0, 256);
            program_identity = Encoding.ASCII.GetString(data, 256, 8);
            version = BinaryTool.ReadInt32(data, 264);

            //Validate the key
            if(!BinaryTool.CompareBytes(Program.master_key, key))
            {
                authenticated = false;
                Log("AUTHENTICATION", "Authentication failed!");
                await DisconnectAsync(WebSocketCloseStatus.PolicyViolation, "DELTA_RPC_DISCONNECT_INVALID_AUTHENTICATION_KEY");
                return;
            }

            //We're now ready for authentication!
            authenticated = true;
            Log("AUTHENTICATION", "Authentication OK!");
            await WriteResponse(0, 0);
        }

        /// <summary>
        /// Handles an RPC message: opcode 1. This will fire events to clients by a filter
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private async Task HandleRPCMessage(byte[] data, int length, ulong index)
        {
            //This follows the following format, starting at byte 10
            //  4 bytes: int - Payload length "x"
            //  x bytes: string - Payload
            //  2 bytes: ushort - Filter type
            //  [Dependant on filter type]

            //Read the length of the payload and read the payload
            int payloadLength = BinaryTool.ReadInt32(data, 10);
            byte[] payload = BinaryTool.CopyFromArray(data, 14, payloadLength);
            Log("RPC-MESSAGE", "Payload length " + payloadLength);

            //Read the filter code
            ushort filterCode = BinaryTool.ReadUInt16(data, 14 + payloadLength);
            Log("RPC-MESSAGE", "Filter type " + filterCode);

            //Pack contents
            PackedWebSocketMessage packed = new PackedWebSocketMessage(payload, payloadLength, WebSocketMessageType.Text);

            //Read the filter and send messages
            Task pending;
            if(filterCode == 0)
            {
                //USER_ID - Read the user ID
                //  12 bytes: MongoDB ID - User ID
                ObjectId userId = BinaryTool.ReadMongoID(data, 14 + payloadLength + 2);
                pending = RPCEventDispatcher.SendDataToUserById(RPCType.RPCSession, userId, packed); 
            }
            else if (filterCode == 1)
            {
                //SERVER - Sends to all members of a server
                //  12 bytes: MongoDB ID - Server ID
                ObjectId serverId = BinaryTool.ReadMongoID(data, 14 + payloadLength + 2);
                pending = RPCEventDispatcher.SendDataToServerById(RPCType.RPCSession, serverId, packed);
            }
            else if (filterCode == 2)
            {
                //SERVER_TRIBE - Sends to a tribe of a server
                //  12 bytes: MongoDB ID - Server ID
                //  4 bytes: Int - ARK Tribe ID
                ObjectId serverId = BinaryTool.ReadMongoID(data, 14 + payloadLength + 2);
                int tribeId = BinaryTool.ReadInt32(data, 14 + payloadLength + 2 + 12);
                pending = RPCEventDispatcher.SendDataToServerTribeById(RPCType.RPCSession, serverId, tribeId, packed);
            } else
            {
                await WriteResponse(index, 400);
                return;
            }

            //Wait for sending to complete
            Log("RPC-MESSAGE", "Sending message....pending...");
            await pending;
            Log("RPC-MESSAGE", "Message sent OK!");

            //Send OK
            await WriteResponse(index, 0);
        }

        private async Task<T> ReadJSON<T>(byte[] data, int index, int length)
        {
            //Read text
            string d = Encoding.UTF8.GetString(data, index, length);
            return JsonConvert.DeserializeObject<T>(d);
        }

        /// <summary>
        /// Writes a standard response: the only thing this will ever respond with
        /// </summary>
        /// <param name="index"></param>
        /// <param name="opcode"></param>
        /// <returns></returns>
        private async Task WriteResponse(ulong index, ushort opcode)
        {
            //This includes just 10 bytes:
            //  8 bytes: The index of the request
            //  2 bytes: The response opcode. 0 means that all is good

            //Create payload
            byte[] payload = new byte[10];
            BinaryTool.WriteUInt64(payload, 0, index);
            BinaryTool.WriteUInt16(payload, 8, opcode);

            //Send
            await SendData(payload);
        }
    }
}
