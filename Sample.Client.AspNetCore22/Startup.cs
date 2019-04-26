﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureADB2C.UI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Sample.Client.AspNetCore22
{
    public class Startup
    {
        public const string SampleApiHttpClientName = "SampleApi";
        public const string ClaimTypeAccessToken = "access_token";
        public const string ClaimTypeRefreshToken = "refresh_token";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Inject an HTTP client for the back-end Web API.
            services.AddHttpClient(SampleApiHttpClientName, c =>
            {
                c.BaseAddress = new Uri(Configuration["SampleApiRootUrl"]);
            });

            // Don't map any standard OpenID Connect claims to Microsoft-specific claims.
            // See https://leastprivilege.com/2017/11/15/missing-claims-in-the-asp-net-core-2-openid-connect-handler/
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            // Add AAD B2C authentication using OpenID Connect.
            services.AddAuthentication(AzureADB2CDefaults.AuthenticationScheme)
                .AddAzureADB2C(options => Configuration.Bind("AzureAdB2C", options));
            services.Configure<OpenIdConnectOptions>(AzureADB2CDefaults.OpenIdScheme, options =>
            {
                // Don't remove any incoming claims.
                options.ClaimActions.Clear();

                // options.GetClaimsFromUserInfoEndpoint = true; // AAD B2C doesn't currently support the UserInfo endpoint.

                options.ResponseType = OpenIdConnectResponseType.CodeIdToken; // Trigger a hybrid OIDC + auth code flow.
                // NOTE: The scopes must be set here so they are requested from the beginning, requesting these during the auth code redemption doesn't work.
                options.Scope.Add("offline_access"); // Request a "refresh_token" as part of the auth code flow.
                // options.Scope.Add(options.ClientId); // Request an "access_token" for the API represented by the same application as part of the auth code flow.
                // NOTE: You cannot request both the <Client ID> scope and a scope of an external API as this would have to result
                // in an access token for multiple audiences which isn't possible.
                // See https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-access-tokens#requesting-a-token
                options.Scope.Add(Configuration["AzureAdB2C:ScopeRead"]); // Request an "access_token" for another API.

                // Handle events.
                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    // NOTE: You can optionally take action before being redirected to the identity provider here.
                    return Task.CompletedTask;
                };

                options.Events.OnAuthorizationCodeReceived = context =>
                {
                    // NOTE: As mentioned above, setting the scope here doesn't work, the access_token doesn't get
                    // returned unless the scope was requested during sign in.
                    // context.TokenEndpointRequest.Scope = options.ClientId + " offline_access";
                    return Task.CompletedTask;
                };

                options.Events.OnTokenResponseReceived = context =>
                {
                    // Normally, the access and refresh tokens that resulted from the authorization code flow would be
                    // stored in a cache like ADAL/MSAL's user cache.
                    // To simplify here, we're adding them as extra claims in the user's claims identity
                    // (which is ultimately encrypted and serialized into the authentication cookie).
                    var identity = (ClaimsIdentity)context.Principal.Identity;
                    identity.AddClaim(new Claim(ClaimTypeAccessToken, context.TokenEndpointResponse.AccessToken));
                    identity.AddClaim(new Claim(ClaimTypeRefreshToken, context.TokenEndpointResponse.RefreshToken));
                    return Task.CompletedTask;
                };

                options.Events.OnTokenValidated = context =>
                {
                    var identity = (ClaimsIdentity)context.Principal.Identity;
                    //context.Properties.IsPersistent = true; // Optionally ensure the cookie is persistent across browser sessions.
                    return Task.CompletedTask;
                };
            });
            services.Configure<CookieAuthenticationOptions>(AzureADB2CDefaults.CookieScheme, options =>
            {
                // Optionally set authentication cookie options here.
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            services.AddRouting(setup => { setup.LowercaseUrls = true; });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}