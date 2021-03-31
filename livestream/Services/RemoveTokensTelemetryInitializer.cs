using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace LivestreamFunctions.Services
{
    // Use telemetry initializers to enrich telemetry with additional properties or override an existing one. Use a telemetry processor to filter out telemetry.
    // https://docs.microsoft.com/en-us/azure/azure-monitor/app/api-filtering-sampling#itelemetryprocessor-and-itelemetryinitializer
    public class RemoveTokensTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is not RequestTelemetry requestTelemetry) return;

            if (requestTelemetry.Url?.Query?.Contains("token") == true)
            {
                var query = HttpUtility.ParseQueryString(requestTelemetry.Url.Query);
                query.Set("token", "removed-from-logs");
                var builder = new UriBuilder(requestTelemetry.Url)
                {
                    Query = query.ToString()
                };
                requestTelemetry.Url = builder.Uri;
            }
        }
    }
}
