using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace VODFunctions.Services
{
    public class StreamingTokenHelper
    {
        private readonly string _JWTVerificationKey;
        public StreamingTokenHelper(string JWTVerificationKey)
        {
            _JWTVerificationKey = JWTVerificationKey;
        }

        public string Generate(DateTimeOffset expiryTime)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.Default.GetBytes(_JWTVerificationKey));
            var signingCredentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(issuer: "https://brunstad.tv", audience: "urn:brunstadtv", notBefore: DateTime.Now.AddMinutes(-5), expires: expiryTime.UtcDateTime, signingCredentials: signingCredentials);
            var handler = new JwtSecurityTokenHandler();
            var tokenString = handler.WriteToken(token);

            return tokenString;
        }

        public bool ValidateToken(string token)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.Default.GetBytes(_JWTVerificationKey));

            var validationParameters =
                new TokenValidationParameters
                {
                    ValidIssuer = "https://brunstad.tv",
                    ValidAudiences = new[] { "urn:brunstadtv" },
                    IssuerSigningKeys = new[] { securityKey },
                    ValidateLifetime = true,
                    LifetimeValidator = LifetimeValidator,
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true
                };

            SecurityToken validatedToken;
            var handler = new JwtSecurityTokenHandler();

            try
            {
                handler.ValidateToken(token, validationParameters, out validatedToken);
            }
            catch (SecurityTokenException)
            {
                return false;
            }

            return validatedToken != null;
        }

        private static bool LifetimeValidator(DateTime? notBefore, DateTime? expires, SecurityToken token, TokenValidationParameters @params)
        {
            if (expires != null)
            {
                return expires > DateTime.UtcNow;
            }
            return false;
        }
    }
}
