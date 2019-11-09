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

        public SenderConnection(Socket sock)
        {
            this.sock = sock;
            buffer = new byte[Program.config.buffer_size * 2];
            is_authenticated = false;
            salt = LibDeltaSystem.Tools.SecureStringTool.GenerateSecureRandomBytes(32);
        }

        /// <summary>
        /// Wait for incoming data
        /// </summary>
        public void BeginReceive()
        {
            sock.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, OnDataReceived, null);
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

                //Handle this data

                if (is_authenticated)
                    HandleDataAuthenticated(len);
                else
                    HandleDataUnauthenticated(len);

                //Listen for next messages
                BeginReceive();
            } catch
            {
                Console.WriteLine("RPC Socket Error");
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
        /// Called when we get data while unauthenticated
        /// </summary>
        /// <param name="length"></param>
        private void HandleDataUnauthenticated(int length)
        {
            //Verify this. First, compute the intended result
            byte[] intended = HMACTool.ComputeHMAC(SenderServer.key, salt, SenderServer.key);

            //Compare
            bool ok = HMACTool.CompareHMAC(intended, buffer);

            //If this failed, shut down the connection
            if (!ok)
                throw new Exception("Authentication failed!");
            is_authenticated = ok;
            Console.WriteLine("Authenticated!");
        }

        /// <summary>
        /// Called when we get data while authenticated
        /// </summary>
        /// <param name="length"></param>
        private void HandleDataAuthenticated(int length)
        {
            //Get the HMAC from the data because we are about to overwrite it
            byte[] hmac = new byte[32];
            Array.Copy(buffer, 0, hmac, 0, 32);

            //Get the message buffer so that we can do conversions on it
            if (length < 32)
                throw new Exception("Data sent is too small!");
            byte[] content = new byte[length - 32];
            Array.Copy(buffer, 32, content, 0, length - 32);

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

            //Now, read all chunks
            byte[][] chunks = new byte[chunkCount][];
            for(int i = 0; i<chunkCount; i++)
            {
                //Read length of the chunk
                int chunkLength = HelperReadInt32(content, offset);
                offset += 4;

                //Read chunk
                chunks[i] = new byte[chunkLength];
                Array.Copy(content, offset, chunks[i], 0, chunkLength);
                offset += chunkLength;
            }

            //Now, handle this
            switch(opcode)
            {
                case 1: HandleRPCSendEvent(chunks); break;
            }
        }

        /// <summary>
        /// Handles an incoming RPC message. Opcode 1
        /// </summary>
        /// <param name="chunks"></param>
        private void HandleRPCSendEvent(byte[][] chunks)
        {
            //Decode the first chunk as a filter
            RPCFilter filter = JsonConvert.DeserializeObject<RPCFilter>(Encoding.UTF8.GetString(chunks[0]));

            //Distribute messages
            SessionHolder.DistributeMessage(filter, chunks[1]);
        }
    }
}
