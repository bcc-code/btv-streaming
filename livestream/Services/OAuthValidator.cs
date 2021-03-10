using LazyCache;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Threading.Tasks;

namespace LivestreamFunctions.Services
{
    public class OAuthValidator
    {
        private readonly IAppCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _oidcAuthority;
        private readonly string _jwksEndpoint;

        public OAuthValidator(IAppCache cache, IHttpClientFactory httpClientFactory, string oidcAuthority, string jwksEndpoint)
        {
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _jwksEndpoint = jwksEndpoint;
            _oidcAuthority = oidcAuthority;
        }

        internal async Task<bool> ValidateToken(string token)
        {
            var jwksJson = await _cache.GetOrAddAsync("jwks", async () => {
                var http = _httpClientFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(5);
                var jwksResponse = await http.GetAsync(_jwksEndpoint);
                return await jwksResponse.Content.ReadAsStringAsync();
            }, DateTimeOffset.Now.AddHours(24));

            var jwks = JsonWebKeySet.Create(jwksJson);

            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = _oidcAuthority,
                ValidateAudience = false,
                IssuerSigningKeys = jwks.Keys,
                ValidateLifetime = true,
                ValidateIssuer = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
            };

            SecurityToken validatedToken;
            var handler = new JwtSecurityTokenHandler();

            try
            {
                handler.ValidateToken(token, validationParameters, out validatedToken);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return validatedToken != null;
        }
    }
}
