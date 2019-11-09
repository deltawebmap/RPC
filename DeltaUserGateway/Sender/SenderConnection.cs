using LibDeltaSystem.RPC;
using LibDeltaSystem.Tools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace DeltaUserGateway.Sender
{
    public class SenderConnection
    {
        /// <summary>
        /// The actual socket we will be using.
        /// </summary>
        public Socket sock;

        /// <summary>
        /// Can we send messages?
        /// </summary>
        public bool is_authenticated;

        /// <summary>
        /// The message buffer
        /// </summary>
        public byte[] buffer;

        /// <summary>
        /// Unique salt set when this is created to protect transport
        /// </summary>
        public byte[] salt;

        /// <summary>
        /// If this is false, we are in the process of downloading the length first
        /// </summary>
        public bool has_length;

        public SenderConnection(Socket sock)
        {
            this.sock = sock;
            buffer = new byte[4];
            is_authenticated = false;
            salt = LibDeltaSystem.Tools.SecureStringTool.GenerateSecureRandomBytes(32);

            //First, listen for the length to be sent to us
            sock.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, OnDataReceived, 0);
        }

        public void Log(string content)
        {
            if (Program.config.debug_mode)
                Console.WriteLine("[SenderConnection -> Debug] " + content);
        }

        /// <summary>
        /// Wait for incoming data
        /// </summary>
        public void BeginReceive()
        {
            sock.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, OnDataReceived, 0);
        }

        /// <summary>
        /// Called when data is downloaded
        /// </summary>
        /// <param name="ar"></param>
        private void OnDataReceived(IAsyncResult ar)
        {
            try
            {
                //Get length
                int len = sock.EndReceive(ar);
                int offset = ((int)ar.AsyncState) + len;

                //If we downloaded no bytes, we have no remaining bytes to download
                if (len > 0)
                {
                    sock.BeginReceive(buffer, offset, buffer.Length - offset, SocketFlags.None, OnDataReceived, offset);
                    return;
                }
                
                //Handle this data
                if(!has_length)
                {
                    //We'll need to download the real content
                    int length = HelperReadInt32(buffer, 0);

                    //There is a maximum size we can use when we aren't authenticated. Check if it is exceeded
                    if (!is_authenticated && length > 100)
                        throw new Exception("Allocated size is too large. Authenticate first before sending a request this large.");

                    //Create buffers
                    Log("About to download " + length);
                    buffer = new byte[length];
                    has_length = true;
                    BeginReceive();
                    return;
                }

                //We have the content.
                HandleDataAuthenticated();

                //Listen for next messages
                has_length = false;
                buffer = new byte[4];
                BeginReceive();
            } catch(Exception ex)
            {
                Log("RPC Socket Error "+ex.Message+ex.StackTrace);
                try
                {
                    sock.Close();
                } catch { }
            }
        }

        private int HelperReadInt32(byte[] buffer, int offset)
        {
            byte[] b = new byte[4];
            Array.Copy(buffer, offset, b, 0, 4);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(b);
            return BitConverter.ToInt32(b);
        }

        /// <summary>
        /// Called when we get data while authenticated
        /// </summary>
        /// <param name="length"></param>
        private void HandleDataAuthenticated()
        {
            //Get the HMAC from the data because we are about to overwrite it
            byte[] hmac = new byte[32];
            Array.Copy(buffer, 0, hmac, 0, 32);

            //Get the message buffer so that we can do conversions on it
            if (buffer.Length < 32)
                throw new Exception("Data sent is too small!");
            byte[] content = new byte[buffer.Length - 32];
            Array.Copy(buffer, 32, content, 0, buffer.Length - 32);

            //Calculate the HMAC
            byte[] intended = HMACTool.ComputeHMAC(SenderServer.key, salt, SenderServer.key, content);
            if (HMACTool.CompareHMAC(intended, buffer))
                throw new Exception("Failed to authenticate!");

            //Now, we can read the data
            int offset = 0;
            int opcode = HelperReadInt32(content, offset);
            offset += 4;
            int chunkCount = HelperReadInt32(content, offset);
            offset += 4;
            Log($"Opcode {opcode}, count {chunkCount}, len {buffer.Length}");

            //Now, read all chunks
            byte[][] chunks = new byte[chunkCount][];
            for(int i = 0; i<chunkCount; i++)
            {
                //Read length of the chunk
                int chunkLength = HelperReadInt32(content, offset);
                Log("Chunk length " + chunkLength);
                offset += 4;

                //Read chunk
                chunks[i] = new byte[chunkLength];
                Array.Copy(content, offset, chunks[i], 0, chunkLength);
                offset += chunkLength;
            }

            //Now, handle this
            switch(opcode)
            {
                case 0: HandleRPCAuthEvent(chunks); break;
                case 1: HandleRPCSendEvent(chunks); break;
                default: throw new Exception($"Unknown opcode {opcode}, this is probably a corrupted message!");
            }
        }

        /// <summary>
        /// Handles an incoming RPC auth message. Opcode 0
        /// </summary>
        /// <param name="chunks"></param>
        private void HandleRPCAuthEvent(byte[][] chunks)
        {
            //Calculate the HMAC
            byte[] intended = HMACTool.ComputeHMAC(SenderServer.key, salt, SenderServer.key);
            if (!HMACTool.CompareHMAC(intended, chunks[0]))
                throw new Exception("Failed to authenticate!");

            //Auth OK!
            is_authenticated = true;
            Log("Authenticated!");
        }

        /// <summary>
        /// Handles an incoming RPC message. Opcode 1
        /// </summary>
        /// <param name="chunks"></param>
        private void HandleRPCSendEvent(byte[][] chunks)
        {
            //Stop if we're not authenticated
            if (!is_authenticated)
                throw new Exception("Attempted to run a command that requires authentication.");
            
            //Decode the first chunk as a filter
            RPCFilter filter = JsonConvert.DeserializeObject<RPCFilter>(Encoding.UTF8.GetString(chunks[0]));

            //Distribute messages
            SessionHolder.DistributeMessage(filter, chunks[1]);
        }
    }
}
