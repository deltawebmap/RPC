using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using LibDeltaSystem.PRC;
using System.Threading.Tasks;
using LibDeltaSystem.Db.Content;

namespace DeltaUserGateway
{
    public static class SessionHolder
    {
        public static ConcurrentBag<RPCSession> sessions = new ConcurrentBag<RPCSession>();

        public static bool TryGetSessionByID(string id, out RPCSession session)
        {
            session = null;
            if (id == null)
                return false;
            if (id.Length == 0)
                return false;
            lock (sessions)
                session = sessions.Where(x => x.id == id).FirstOrDefault();
            return session != null;
        }

        public static RPCSession MakeSession()
        {
            string id;
            RPCSession session;
            lock (sessions)
            {
                //Create ID
                id = LibDeltaSystem.Tools.SecureStringTool.GenerateSecureString(24);
                while (TryGetSessionByID(id, out session))
                    id = LibDeltaSystem.Tools.SecureStringTool.GenerateSecureString(24);

                //Add session 
                session = new RPCSession(id);
                sessions.Add(session);
            }
            return session;
        }

        /// <summary>
        /// Sends messages according to the filter
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="data"></param>
        public static async Task DistributeMessage(RPCFilter filter, byte[] data)
        {
            //We'll need an array of user IDs. Obtain those first.
            List<RPCSession> sessions;
            if(filter.type == "USER_ID")
            {
                //The user ID is in the payload
                sessions = GetUsersByUserID(new string[] { filter.keys["USER_ID"] });
            } else if(filter.type == "SERVER" || filter.type == "TRIBE")
            {
                //Get all users of the server
                List<DbPlayerProfile> profiles;
                if(filter.type == "SERVER")
                    profiles = await Program.conn.GetServerPlayerProfilesAsync(filter.keys["SERVER_ID"]);
                else
                    profiles = await Program.conn.GetServerPlayerProfilesByTribeAsync(filter.keys["SERVER_ID"], int.Parse(filter.keys["TRIBE_ID"]));

                //Get a list of steam IDs
                List<string> steamIds = new List<string>();
                foreach(var p in profiles)
                {
                    if (!steamIds.Contains(p.steam_id))
                        steamIds.Add(p.steam_id);
                }

                //Find all users
                sessions = GetUsersBySteamID(steamIds.ToArray());
            } else
            {
                throw new Exception("Unknown filter.");
            }

            //Now, send this message to all of these sessions
            foreach(var u in sessions)
            {
                try
                {
                    u.SendMessage(data);
                }
                catch { }
            }
        }

        /// <summary>
        /// Finds users by their Steam ID
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="data"></param>
        private static List<RPCSession> GetUsersBySteamID(string[] steamIds)
        {
            List<RPCSession> targets;
            lock (sessions)
                targets = sessions.Where(x => steamIds.Contains(x.steam_id)).ToList();
            return targets;
        }

        /// <summary>
        /// Finds users by their User ID
        /// </summary>
        /// <param name="userIds"></param>
        /// <param name="data"></param>
        private static List<RPCSession> GetUsersByUserID(string[] userIds)
        {
            List<RPCSession> targets;
            lock (sessions)
                targets = sessions.Where(x => userIds.Contains(x.user_id)).ToList();
            return targets;
        }
    }
}
