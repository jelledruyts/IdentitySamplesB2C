using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureADB2C.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Sample.Client.AspNetCore22.Models;

namespace Sample.Client.AspNetCore22.Controllers
{
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly string signingSecret;
        private readonly string invitationPolicyId;

        public AccountController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            this.httpClientFactory = httpClientFactory;
            this.signingSecret = configuration["AzureAdB2C:ClientSecret"];
            this.invitationPolicyId = configuration["AzureAdB2C:InvitationPolicyId"];
        }

        public async Task<IActionResult> Identity()
        {
            var relatedApplicationIdentities = new List<IdentityInfo>();
            try
            {
                // Request identity information as seen by the back-end Web API.
                var client = this.httpClientFactory.CreateClient(Startup.SampleApiHttpClientName);
                // Fetch the access token from the current user's claims to avoid the complexity of an external token cache (see Startup.cs).
                var accessTokenClaim = this.User.Claims.SingleOrDefault(c => c.Type == Startup.ClaimTypeAccessToken);
                if (accessTokenClaim != null)
                {
                    // Call the back-end Web API using the bearer access token.
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessTokenClaim.Value);
                    var response = await client.GetAsync("api/identity");
                    response.EnsureSuccessStatusCode();

                    // Deserialize the response into an IdentityInfo instance.
                    var apiIdentityInfoValue = await response.Content.ReadAsStringAsync();
                    var apiIdentityInfo = JsonConvert.DeserializeObject<IdentityInfo>(apiIdentityInfoValue);
                    relatedApplicationIdentities.Add(apiIdentityInfo);
                }
            }
            catch (Exception exc)
            {
                relatedApplicationIdentities.Add(new IdentityInfo
                {
                    Source = "Exception",
                    Application = "Sample API",
                    IsAuthenticated = false,
                    Claims = new[] { new ClaimInfo { Type = "ExceptionMessage", Value = exc.Message }, new ClaimInfo { Type = "ExceptionDetail", Value = exc.ToString() } }
                });
            }
            // Return identity information as seen from this application, including related applications.
            var identityInfo = new IdentityInfo
            {
                Source = "ID Token",
                Application = "Sample Client",
                IsAuthenticated = this.User.Identity.IsAuthenticated,
                Name = this.User.Identity.Name,
                AuthenticationType = this.User.Identity.AuthenticationType,
                Claims = this.User.Claims.Select(c => new ClaimInfo { Type = c.Type, Value = c.Value }).ToList(),
                RelatedApplicationIdentities = relatedApplicationIdentities
            };
            return View(identityInfo);
        }

        public IActionResult Invite()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Invite(string email, int validDays = 30)
        {
            if (!string.IsNullOrEmpty(email))
            {
                // Generate an invitation link for the requested email address by generating a self-issued token and sending that to the "Register" action.
                var expiration = TimeSpan.FromDays(validDays); // Defines how long the invitation is valid.
                var claims = new[]
                {
                    new Claim("verified_email", email) // This claim maps to the extension attribute registered in AAD B2C which is used in the custom invitation policy.
                };
                var selfIssuedToken = CreateSelfIssuedToken(expiration, claims, this.signingSecret);

                var authenticationRequestUrl = Url.Action("Register", "Account", new { client_assertion = selfIssuedToken }, "https" /* This forces an absolute URL */);
                this.ViewData["Email"] = email;
                this.ViewData["AuthenticationRequestUrl"] = authenticationRequestUrl;
                return View();
            }
            return RedirectToAction("Invite");
        }

        public async Task<IActionResult> Register(string client_assertion)
        {
            if (string.IsNullOrWhiteSpace(client_assertion))
            {
                return BadRequest();
            }
            // Tell the AAD B2C middleware to invoke a specific policy and pass the client assertion through.
            var authenticationProperties = new AuthenticationProperties();
            authenticationProperties.RedirectUri = Url.Action("Registered", "Account");
            // NOTE: this resets the scope and response type so no authorization code is requested from this flow,
            // see https://github.com/aspnet/AspNetCore/blob/release/2.2/src/Azure/AzureAD/Authentication.AzureADB2C.UI/src/AzureAdB2COpenIDConnectEventHandlers.cs#L35-L36.
            authenticationProperties.Items[AzureADB2CDefaults.PolicyKey] = this.invitationPolicyId;
            authenticationProperties.Items[OpenIdConnectParameterNames.ClientAssertion] = client_assertion;
            await HttpContext.ChallengeAsync(AzureADB2CDefaults.AuthenticationScheme, authenticationProperties);
            return new EmptyResult();
        }

        [Authorize]
        public IActionResult Registered()
        {
            return View();
        }

        internal static string CreateSelfIssuedToken(TimeSpan expiration, ICollection<Claim> claims, string signingSecret)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var nowUtc = DateTime.UtcNow;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret));
            var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Audience = "my-audience", // Not important as we are self-issuing the token.
                Expires = nowUtc.Add(expiration),
                IssuedAt = nowUtc,
                Issuer = "https://my-issuer", // Not important as we are self-issuing the token.
                NotBefore = nowUtc,
                SigningCredentials = signingCredentials,
                Subject = new ClaimsIdentity(claims)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}