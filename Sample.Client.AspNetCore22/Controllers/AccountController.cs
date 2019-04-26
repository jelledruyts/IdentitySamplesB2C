using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Sample.Client.AspNetCore22.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace Sample.Client.AspNetCore22.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory httpClientFactory;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        [Route("Account")]
        public async Task<IActionResult> Index()
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
                    Claims = new Dictionary<string, string> { { "ExceptionMessage", exc.Message }, { "ExceptionDetail", exc.ToString() } }
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
                Claims = this.User.Claims.ToDictionary(c => c.Type, c => c.Value),
                RelatedApplicationIdentities = relatedApplicationIdentities
            };
            return View(identityInfo);
        }
    }
}