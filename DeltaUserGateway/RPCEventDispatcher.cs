using DeltaUserGateway.Queries;
using LibDeltaSystem.WebFramework.WebSockets.Entities;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DeltaUserGateway
{
    public static class RPCEventDispatcher
    {
        public static async Task<int> SendDataToUserById(RPCType type, ObjectId id, PackedWebSocketMessage message)
        {
            //Find the group
            var group = Program.holder.FindGroup(new RPCGroupQueryUser
            {
                type = type,
                user_id = id
            });
            if (group == null)
                return 0;

            //Send event to all
            await group.SendDistributedMessage(message, new List<LibDeltaSystem.WebFramework.WebSockets.Groups.GroupWebSocketService>());

            return group.clients.Count;
        }

        public static async Task<int> SendDataToServerById(RPCType type, ObjectId id, PackedWebSocketMessage message)
        {
            //Find the group
            var group = Program.holder.FindGroup(new RPCGroupQueryServer
            {
                type = type,
                server_id = id
            });
            if (group == null)
                return 0;

            //Send event to all
            await group.SendDistributedMessage(message, new List<LibDeltaSystem.WebFramework.WebSockets.Groups.GroupWebSocketService>());

            return group.clients.Count;
        }

        public static async Task<int> SendDataToServerTribeById(RPCType type, ObjectId id, int tribe_id, PackedWebSocketMessage message)
        {
            //Find the group
            var group = Program.holder.FindGroup(new RPCGroupQueryServerTribe
            {
                type = type,
                server_id = id,
                tribe_id = tribe_id
            });
            if (group == null)
                return 0;

            //Send event to all
            await group.SendDistributedMessage(message, new List<LibDeltaSystem.WebFramework.WebSockets.Groups.GroupWebSocketService>());

            return group.clients.Count;
        }
    }
}
