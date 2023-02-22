using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BrunstadTV.VOD.WebClient.Authorization
{
    public class MembershipAuthorizationRequirement : IAuthorizationRequirement { }

    public class MembershipAuthorizationRequirementHandler : AuthorizationHandler<MembershipAuthorizationRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, MembershipAuthorizationRequirement requirement)
        {
            var json = context.User?.Claims?.FirstOrDefault(c => c.Type == "https://members.bcc.no/app_metadata");
            try
            {
                var appMetadata = JsonConvert.DeserializeAnonymousType(json.Value, new { hasMembership = false });
                if (appMetadata != null && appMetadata.hasMembership)
                {
                    context.Succeed(requirement);
                }
            }
            catch (Exception) { }
            return Task.CompletedTask;
        }
    }
}