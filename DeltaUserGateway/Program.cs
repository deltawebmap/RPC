using DeltaUserGateway.Definitions;
using LibDeltaSystem;
using LibDeltaSystem.WebFramework;
using LibDeltaSystem.WebFramework.WebSockets.Groups;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeltaUserGateway
{
    class Program
    {
        public static GatewayConfig config;
        public static DeltaConnection conn;
        public static WebSocketGroupHolder holder;

        public static byte[] master_key; //The key to connect

        static void Main(string[] args)
        {
            //Load config
            config = JsonConvert.DeserializeObject<GatewayConfig>(File.ReadAllText(args[0])); 

            //Launch server
            holder = new WebSocketGroupHolder();
            MainAsync().GetAwaiter().GetResult();
        }

        public static async Task MainAsync()
        {
            //Connect to database
            conn = new DeltaConnection(config.database_config, "rpc-prod", 1, 0);
            await conn.Connect();
            master_key = Convert.FromBase64String(conn.config.rpc_key);

            //Start server
            DeltaWebServer server = new DeltaWebServer(conn, config.port);
            server.AddService(new SenderDefinition());
            server.AddService(new RPCSessionDefinition());
            await server.RunAsync();
        }

        /*public static async Task AcceptRootRequest(Microsoft.AspNetCore.Http.HttpContext e)
        {
            await WriteStringToStreamAsync(e.Response.Body, "DeltaWebMap RPC Server\n\nhttps://github.com/deltawebmap/RPC\n\n(C) DeltaWebMap 2020, RomanPort 2020");
        }*/
    }
}
