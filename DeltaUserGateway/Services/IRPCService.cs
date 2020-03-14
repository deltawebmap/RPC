using DeltaUserGateway.Queries;
using LibDeltaSystem;
using LibDeltaSystem.Db.Content;
using LibDeltaSystem.Db.System;
using LibDeltaSystem.WebFramework.WebSockets.Groups;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DeltaUserGateway.Services
{
    public class IRPCService : UserAuthenticatedGroupWebSocketService
    {
        public List<Tuple<DbServer, DbPlayerProfile>> servers;

        public IRPCService(DeltaConnection conn, HttpContext e) : base(conn, e)
        {
        }

        public override async Task<bool> OnPreRequest()
        {
            //Call base
            if (!await base.OnPreRequest())
                return false;

            //Fetch user servers
            servers = await user.GetGameServersAsync(conn);
            return true;
        }

        public override async Task<List<WebSocketGroupQuery>> AuthenticateGroupsQuery()
        {
            var queries = new List<WebSocketGroupQuery>();

            //Get type
            RPCType type = RPCType.RPCSession;

            //Add user query
            queries.Add(new RPCGroupQueryUser
            {
                type = type,
                user_id = user._id
            });

            //Add server queries
            foreach(var s in servers)
            {
                queries.Add(new RPCGroupQueryServer
                {
                    type = type,
                    server_id = s.Item1._id
                });
                queries.Add(new RPCGroupQueryServerTribe
                {
                    type = type,
                    server_id = s.Item1._id,
                    tribe_id = s.Item2.tribe_id,
                    any_tribe_id = s.Item1.CheckIsUserAdmin(user)
                });
            }

            return queries;
        }

        public override WebSocketGroupHolder GetGroupHolder()
        {
            return Program.holder;
        }

        public override async Task OnReceiveBinary(byte[] data, int length)
        {
            
        }

        public override async Task OnReceiveText(string data)
        {
            Console.WriteLine("e");
            string s = "";
            foreach(var g in groups)
            {
                s += g.identifier.GetType().Name + " / " + g.clients.Count + "\n";
            }
            await SendData(s);
        }

        public override async Task<bool> SetArgs(Dictionary<string, string> args)
        {
            return true;
        }
    }
}
