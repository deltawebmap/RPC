using LibDeltaSystem.WebFramework.WebSockets.Groups;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeltaUserGateway.Queries
{
    public class IRPCGroupQuery : WebSocketGroupQuery
    {
        public RPCType type;
        
        public override bool CheckIfAuthorized(WebSocketGroupQuery request)
        {
            //Check if the type matches
            if (((IRPCGroupQuery)request).type != type)
                return false;

            //Make sure the type of group matches
            if (request.GetType() != typeof(IRPCGroupQuery))
                return false;

            return true;
        }
    }
}
