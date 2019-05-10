using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sample.Api.AspNetCore22
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Read the API's published scope names from configuration.
            Constants.Scopes.IdentityRead = Configuration["AzureAdB2C:Scopes:IdentityRead"];
            Constants.Scopes.IdentityReadWrite = Configuration["AzureAdB2C:Scopes:IdentityReadWrite"];

            // Don't map any standard OpenID Connect claims to Microsoft-specific claims.
            // See https://leastprivilege.com/2017/11/15/missing-claims-in-the-asp-net-core-2-openid-connect-handler/
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = $"{Configuration["AzureAdB2C:Instance"]}{Configuration["AzureAdB2C:Domain"]}/{Configuration["AzureAdB2C:PolicyId"]}/v2.0/";
                    options.Audience = Configuration["AzureAdB2C:ClientId"];

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            // NOTE: You can optionally take action when the OAuth 2.0 bearer token was validated.
                            return Task.CompletedTask;
                        },
                        OnAuthenticationFailed = context =>
                        {
                            // NOTE: You can optionally take action when the OAuth 2.0 bearer token was rejected.
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Constants.AuthorizationPolicies.Baseline, b =>
                {
                    // An authenticated user (i.e. an incoming JWT bearer token) is always required.
                    b.RequireAuthenticatedUser();
                    // A "scope" claim is also required, if not any application could simply request
                    // a valid access token to call into this API without being authorized.
                    b.RequireClaim(Constants.ClaimTypes.Scope);
                });
                options.AddPolicy(Constants.AuthorizationPolicies.ReadIdentity, b =>
                {
                    b.RequireAssertion(context =>
                    {
                        // The scopes are emitted in a single claim, separated by a space.
                        var scopeClaims = context.User.Claims.Where(c => c.Type == Constants.ClaimTypes.Scope).SelectMany(c => c.Value.Split(' '));
                        return scopeClaims.Any(c => c == Constants.Scopes.IdentityRead);
                    });
                });
            });

            // Allow CORS for any origin for this demo.
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.AllowAnyOrigin();
                    builder.AllowAnyHeader();
                });
            });

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddMvcOptions(options =>
                {
                    // Enforce the "baseline" policy at the minimum for all requests.
                    options.Filters.Add(new AuthorizeFilter(Constants.AuthorizationPolicies.Baseline));
                });
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
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseCors();
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}
