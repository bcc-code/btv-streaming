using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace LivestreamFunctions.Services
{
    // Use telemetry initializers to enrich telemetry with additional properties or override an existing one. Use a telemetry processor to filter out telemetry.
    // https://docs.microsoft.com/en-us/azure/azure-monitor/app/api-filtering-sampling#itelemetryprocessor-and-itelemetryinitializer
    public class UserAgentTelemetryInitializer : ITelemetryInitializer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserAgentTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is not RequestTelemetry requestTelemetry || _httpContextAccessor.HttpContext == null)
            {
                return;
            }

            requestTelemetry.Context.User.UserAgent = _httpContextAccessor.HttpContext.Request.Headers[HeaderNames.UserAgent];
        }
    }
}
