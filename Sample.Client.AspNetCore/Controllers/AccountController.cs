using Sample.Client.AspNetCore.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureADB2C.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Sample.Client.AspNetCore.Controllers
{
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly string signingSecret;
        private readonly string invitationClientAssertionPolicyId;
        private readonly string invitationCodePolicyId;
        private readonly string userInvitationApiUrl;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public AccountController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            this.httpClientFactory = httpClientFactory;
            this.signingSecret = configuration["AzureAdB2C:ClientSecret"];
            this.invitationClientAssertionPolicyId = configuration["AzureAdB2C:InvitationClientAssertionPolicyId"];
            this.invitationCodePolicyId = configuration["AzureAdB2C:InvitationCodePolicyId"];
            this.userInvitationApiUrl = configuration["UserInvitationApiUrl"];
            this.jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
                    var apiIdentityInfo = JsonSerializer.Deserialize<IdentityInfo>(apiIdentityInfoValue, this.jsonSerializerOptions);
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
            var model = new AccountInvitationViewModel
            {
                CanInviteUsingClientAssertion = !string.IsNullOrWhiteSpace(this.signingSecret),
                CanInviteUsingInvitationCode = !string.IsNullOrWhiteSpace(this.userInvitationApiUrl)
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult InviteUsingClientAssertion(string email, int validDays = 30)
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
                var model = new AccountInvitationViewModel
                {
                    CanInviteUsingClientAssertion = !string.IsNullOrWhiteSpace(this.signingSecret),
                    CanInviteUsingInvitationCode = !string.IsNullOrWhiteSpace(this.userInvitationApiUrl),
                    Email = email,
                    AuthenticationRequestUrl = authenticationRequestUrl
                };
                return View(nameof(Invite), model);
            }
            return RedirectToAction(nameof(Invite));
        }

        [HttpPost]
        public async Task<IActionResult> InviteUsingInvitationCode(string companyId)
        {
            if (!string.IsNullOrEmpty(this.userInvitationApiUrl))
            {
                var client = this.httpClientFactory.CreateClient();
                var invitationCodeRequest = new { CompanyId = companyId };
                var invitationCodeRequestContent = new StringContent(JsonSerializer.Serialize(invitationCodeRequest, this.jsonSerializerOptions), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(this.userInvitationApiUrl, invitationCodeRequestContent);
                response.EnsureSuccessStatusCode();

                var invitationCode = default(string);
                var invitationCodeResponseValue = await response.Content.ReadAsStringAsync();
                if (JsonDocument.Parse(invitationCodeResponseValue).RootElement.TryGetProperty("invitationCode", out var invitationCodeProperty))
                {
                    invitationCode = invitationCodeProperty.GetString();
                }

                var authenticationRequestUrl = Url.Action("Register", "Account", null, "https" /* This forces an absolute URL */);
                var model = new AccountInvitationViewModel
                {
                    CanInviteUsingClientAssertion = !string.IsNullOrWhiteSpace(this.signingSecret),
                    CanInviteUsingInvitationCode = !string.IsNullOrWhiteSpace(this.userInvitationApiUrl),
                    CompanyId = companyId,
                    AuthenticationRequestUrl = authenticationRequestUrl,
                    InvitationCode = invitationCode
                };
                return View(nameof(Invite), model);
            }
            return RedirectToAction(nameof(Invite));
        }

        public async Task<IActionResult> Register(string client_assertion)
        {
            // Tell the AAD B2C middleware to invoke a specific policy.
            // NOTE: this resets the scope and response type so no authorization code is requested from this flow,
            // see https://github.com/aspnet/AspNetCore/blob/release/3.1/src/Azure/AzureAD/Authentication.AzureADB2C.UI/src/AzureAdB2COpenIDConnectEventHandlers.cs#L35-L36.
            var authenticationProperties = new AuthenticationProperties();
            authenticationProperties.RedirectUri = Url.Action("Registered", "Account");

#pragma warning disable 0618 // AzureADB2CDefaults is obsolete in favor of "Microsoft.Identity.Web"
            if (!string.IsNullOrWhiteSpace(client_assertion))
            {
                // Use the client assertion flow and pass the client_assertion through.
                authenticationProperties.Items[AzureADB2CDefaults.PolicyKey] = this.invitationClientAssertionPolicyId;
                authenticationProperties.Items[OpenIdConnectParameterNames.ClientAssertion] = client_assertion;
            }
            else
            {
                // Use the invitation code flow.
                authenticationProperties.Items[AzureADB2CDefaults.PolicyKey] = this.invitationCodePolicyId;
            }
            await HttpContext.ChallengeAsync(AzureADB2CDefaults.AuthenticationScheme, authenticationProperties);
#pragma warning restore 0618
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