using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sample.Api.AspNetCore22.Models;

namespace Sample.Api.AspNetCore22.Controllers
{
    [Authorize(Constants.AuthorizationPolicies.ReadIdentity)]
    [Route("api/[controller]")]
    [ApiController]
    public class IdentityController : ControllerBase
    {
        [HttpGet]
        public ActionResult<IdentityInfo> Get()
        {
            // Return identity information as seen from this application.
            return new IdentityInfo
            {
                Source = "Access Token",
                Application = "Sample API",
                IsAuthenticated = this.User.Identity.IsAuthenticated,
                Name = this.User.Identity.Name,
                AuthenticationType = this.User.Identity.AuthenticationType,
                Claims = this.User.Claims.ToDictionary(c => c.Type, c => c.Value),
                RelatedApplicationIdentities = Array.Empty<IdentityInfo>()
            };
        }
    }
}
