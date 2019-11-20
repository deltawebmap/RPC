using DeltaUserGateway.Sender;
using LibDeltaSystem;
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
        public static SenderServerV2 sender;

        static void Main(string[] args)
        {
            //Load config
            config = JsonConvert.DeserializeObject<GatewayConfig>(File.ReadAllText(args[0]));

            //Launch server
            MainAsync().GetAwaiter().GetResult();
        }

        public static async Task MainAsync()
        {
            //Connect to database
            conn = new DeltaConnection(config.database_config, "rpc-prod", 0, 0);
            await conn.Connect();

            //Start the sender server
            //Sender.SenderServer.StartServer(Convert.FromBase64String(conn.config.rpc_key), conn.config.rpc_port);
            sender = new SenderServerV2(conn, Convert.FromBase64String(conn.config.rpc_key), conn.config.rpc_port);
            sender.StartServer();

            //Set up web server
            var host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    IPAddress addr = IPAddress.Any;
                    options.Listen(addr, config.port);

                })
                .UseStartup<Program>()
                .Build();

            await host.RunAsync();
        }

        public void Configure(IApplicationBuilder app)
        {
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(config.timeout_seconds),
                ReceiveBufferSize = config.buffer_size
            };

            app.UseWebSockets(webSocketOptions);

            app.Run(OnHttpRequest);
        }

        public static async Task WriteStringToStreamAsync(Stream s, string content)
        {
            byte[] data = Encoding.UTF8.GetBytes(content);
            await s.WriteAsync(data);
        }

        public static async Task OnHttpRequest(Microsoft.AspNetCore.Http.HttpContext e)
        {
            try
            {
                if (e.Request.Path == "/v1")
                    await AcceptV1Request(e);
                else if (e.Request.Path == "/")
                    await AcceptRootRequest(e);
                else
                {
                    e.Response.StatusCode = 404;
                    await WriteStringToStreamAsync(e.Response.Body, "Not Found");
                }
            } catch (Exception ex)
            {
                //Log and display error
                var error = await conn.LogHttpError(ex, new System.Collections.Generic.Dictionary<string, string>());
                e.Response.StatusCode = 500;
                await WriteStringToStreamAsync(e.Response.Body, JsonConvert.SerializeObject(error, Newtonsoft.Json.Formatting.Indented));
            }
        }

        public static async Task AcceptV1Request(Microsoft.AspNetCore.Http.HttpContext e)
        {
            //First, authenticate this user
            RPCSession session = await RPCSession.AuthenticateSession(e.Request.Query["access_token"], e.Request.Query["session_id"]);
            if (session == null)
            {
                //Failed to authenticate
                e.Response.StatusCode = 401;
                return;
            }

            //Accept the socket
            var socket = await e.WebSockets.AcceptWebSocketAsync();

            //Set up the client
            WebsocketClient wc = new WebsocketClient();
            await wc.Attach(session);
            await wc.Run(socket);
        }

        public static async Task AcceptRootRequest(Microsoft.AspNetCore.Http.HttpContext e)
        {
            await WriteStringToStreamAsync(e.Response.Body, "DeltaWebMap RPC Server\n\nhttps://github.com/deltawebmap/RPC\n\n(C) DeltaWebMap 2019, RomanPort 2019");
        }
    }
}
