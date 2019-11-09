using System;
using System.Collections.Generic;
using System.Text;

namespace DeltaUserGateway
{
    public class GatewayConfig
    {
        public string database_config;

        public string key;

        public bool debug_mode;

        public int port;
        public int buffer_size;
        public int timeout_seconds;
    }
}
