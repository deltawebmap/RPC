using LibDeltaSystem.Db.System;
using LibDeltaSystem.RPC;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DeltaUserGateway
{
    /// <summary>
    /// Represents an actual session held.
    /// </summary>
    public class RPCSession
    {
        /// <summary>
        /// The actual websocket used as a transport
        /// </summary>
        public WebsocketClient transport;

        /// <summary>
        /// Output queue
        /// </summary>
        public ConcurrentQueue<byte[]> queue;

        /// <summary>
        /// Unique ID of this session
        /// </summary>
        public string id;

        /// <summary>
        /// User ID owning this session
        /// </summary>
        public string user_id;

        /// <summary>
        /// Steam ID owning this session
        /// </summary>
        public string steam_id;

        /// <summary>
        /// The type of RPC session this is
        /// </summary>
        public RPCType type;

        public RPCSession(string id, RPCType type)
        {
            this.id = id;
            this.type = type;
            this.queue = new ConcurrentQueue<byte[]>();
        }

        /// <summary>
        /// Gets a session
        /// </summary>
        /// <param name="access_token">User access token. Should always be valid.</param>
        /// <param name="session_id">Session ID. May or not be valid.</param>
        /// <returns></returns>
        public static async Task<RPCSession> AuthenticateSession(string access_token, string session_id, RPCType type)
        {
            //Authenticate this user
            DbUser user = await Program.conn.AuthenticateUserToken(access_token);
            if (user == null)
                return null;

            //Search for this session in the list of sessions.
            RPCSession session = null;
            if (SessionHolder.TryGetSessionByID(session_id, type, out session))
                return session;

            //Generate a unique ID and add a session
            session = SessionHolder.MakeSession(type);

            //Set some data on the session
            session.user_id = user.id;
            session.steam_id = user.steam_id;

            return session;
        }

        /// <summary>
        /// Used when data is sent.
        /// </summary>
        /// <param name="data"></param>
        public void OnMessageReceived(byte[] data)
        {

        }

        /// <summary>
        /// Sends data to the client.
        /// </summary>
        /// <param name="data"></param>
        public void SendMessage(byte[] data)
        {
            //Add to queue
            queue.Enqueue(data);

            //If the connection is created, try to send this
            if (transport != null)
                transport.Flush();
        }

        /// <summary>
        /// Sends data to the client.
        /// </summary>
        /// <param name="data"></param>
        public void SendMessage(string data)
        {
            SendMessage(Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Sends an RPC message to the client
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="opcode"></param>
        /// <param name="target_server"></param>
        public void SendMessage(RPCPayload payload, RPCOpcode opcode, string target_server)
        {
            RPCMessageContainer msg = new RPCMessageContainer
            {
                opcode = opcode,
                payload = payload,
                target_server = target_server,
                source = "rpc-prod"
            };
            SendMessage(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
        }
    }
}
