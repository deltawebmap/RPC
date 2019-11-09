using LibDeltaSystem.RPC.Payloads;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeltaUserGateway
{
    /// <summary>
    /// Used to handle communications. Contributers: If you know how to propertly do this, PLEASE refactor this! I beg of you!
    /// </summary>
    public class WebsocketClient
    {
        /// <summary>
        /// The socket used.
        /// </summary>
        private WebSocket sock;

        /// <summary>
        /// The connected sessiom
        /// </summary>
        private RPCSession session;

        /// <summary>
        /// Task to send data on the wire
        /// </summary>
        private Task sendTask;

        
        public WebsocketClient()
        {
            sendTask = Task.CompletedTask;
        }
        
        /// <summary>
        /// Accepts an incoming connection.
        /// </summary>
        /// <param name="sock">Websocket used.</param>
        /// <returns></returns>
        public async Task Run(WebSocket sock)
        {
            this.sock = sock;
            Console.WriteLine("Opening");

            //Flush queue
            Flush();

            //Start loops
            await ReadLoop();

            //Shut down the connection
            await Close();
        }

        /// <summary>
        /// Attaches the client to the session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public async Task Attach(RPCSession session)
        {
            session.transport = this;
            this.session = session;

            //Send set session ID
            session.SendMessage(new RPCPayloadRPCSetSessionID
            {
                session_id = session.id,
                host = "rpc-prod"
            }, LibDeltaSystem.RPC.RPCOpcode.RPCSetSessionID, null);

            //Flush queue
            Flush();
        }

        /// <summary>
        /// Detaches from the active client
        /// </summary>
        /// <returns></returns>
        public async Task Detach()
        {
            if (session != null)
                session.transport = null;
            this.session = null;
        }

        /// <summary>
        /// Flushes new buffer messages. Should be used every time we add to the queue
        /// </summary>
        public void Flush()
        {
            lock(sendTask)
            {
                //Ignore if we're already flushing
                if (!sendTask.IsCompleted)
                    return;

                //Trigger
                sendTask = SendQueuedMessages();
            }
        }

        /// <summary>
        /// Loop for reading from the websocket
        /// </summary>
        /// <returns></returns>
        private async Task ReadLoop()
        {
            try
            {
                var buffer = new byte[Program.config.buffer_size];
                WebSocketReceiveResult result = await sock.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                while (!result.CloseStatus.HasValue)
                {
                    Console.WriteLine("Got Msg");
                    result = await sock.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                //We might log this in the future.
            }
        }

        /// <summary>
        /// Sends any messages queued. Should be used every time we add to the queue
        /// </summary>
        /// <returns></returns>
        private async Task SendQueuedMessages()
        {
            //Make sure we have a session
            if (session == null)
                return;

            //Make sure that we're still connected
            if (sock.CloseStatus.HasValue)
                return;

            //Lock queue and send all
            byte[] data;
            while(session.queue.TryDequeue(out data))
            {
                //Send on the network
                try
                {
                    await sock.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
                } catch
                {
                    //Add back, trigger fault
                    session.queue.Enqueue(data);
                    await Close();
                    return;
                }
            }
        }

        /// <summary>
        /// Ends the connection.
        /// </summary>
        /// <returns></returns>
        public async Task Close()
        {
            Console.WriteLine("Closing");

            //Detach
            await Detach();

            //Close
            if (!sock.CloseStatus.HasValue)
            {
                //Signal that this has closed
                try
                {
                    await sock.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
                }
                catch { }
            }
        }
    }
}
