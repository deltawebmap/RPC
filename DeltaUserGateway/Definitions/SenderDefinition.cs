using DeltaUserGateway.Services;
using LibDeltaSystem;
using LibDeltaSystem.WebFramework;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeltaUserGateway.Definitions
{
    public class SenderDefinition : DeltaWebServiceDefinition
    {
        public override string GetTemplateUrl()
        {
            return "/internal/sender";
        }

        public override DeltaWebService OpenRequest(DeltaConnection conn, HttpContext e)
        {
            return new SenderService(conn, e);
        }
    }
}
