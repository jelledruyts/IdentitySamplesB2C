using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sample.Api.AspNetCore
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
            // Read the API's published scope and role names from configuration.
            Constants.Scopes.IdentityRead = Configuration["AzureAdB2C:Scopes:IdentityRead"];
            Constants.Scopes.IdentityReadWrite = Configuration["AzureAdB2C:Scopes:IdentityReadWrite"];
            Constants.Roles.IdentityReader = Configuration["AzureAdB2C:Roles:IdentityReader"];

            // Don't map any standard OpenID Connect claims to Microsoft-specific claims.
            // See https://leastprivilege.com/2017/11/15/missing-claims-in-the-asp-net-core-2-openid-connect-handler/
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            var b2cAuthenticationScheme = JwtBearerDefaults.AuthenticationScheme;
            var aadAuthenticationScheme = JwtBearerDefaults.AuthenticationScheme + "-AAD";
            services.AddAuthentication(b2cAuthenticationScheme)
                // Add the primary JWT bearer configuration for an Azure AD B2C policy.
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
                })
                // Add a secondary JWT bearer configuration for the underlying Azure AD tenant endpoint
                // that is used by the OAuth 2.0 Client Credentials grant.
                // See https://docs.microsoft.com/en-us/aspnet/core/security/authorization/limitingidentitybyscheme#use-multiple-authentication-schemes.
                .AddJwtBearer(aadAuthenticationScheme, options =>
                {
                    options.Authority = $"https://login.microsoftonline.com/{Configuration["AzureAdB2C:Domain"]}/v2.0/";
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
                // Define a policy that allows clients to read identity information.
                options.AddPolicy(Constants.AuthorizationPolicies.ReadIdentity, b =>
                {
                    // Ensure to enforce this on both authentication schemes.
                    b.AddAuthenticationSchemes(b2cAuthenticationScheme, aadAuthenticationScheme);

                    // The policy is allowed if the user has granted consent to an "Identity.Read" scope,
                    // or (in the case of an application token with client credentials) if the client
                    // has the "Identity.Reader" role.
                    b.RequireAssertion(context =>
                    {
                        // The scopes are emitted in a single claim, separated by a space.
                        var scopeClaims = context.User.Claims.Where(c => c.Type == Constants.ClaimTypes.Scope).SelectMany(c => c.Value.Split(' '));
                        var roleClaims = context.User.Claims.Where(c => c.Type == Constants.ClaimTypes.Roles).Select(c => c.Value);
                        return scopeClaims.Any(c => c == Constants.Scopes.IdentityRead) || roleClaims.Any(c => c == Constants.Roles.IdentityReader);
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

            services.AddControllers()
                .AddMvcOptions(options =>
                {
                    // Enforce a "baseline" policy at the minimum for all requests.
                    // Note that this is different from specifying AuthorizationOptions.DefaultPolicy, which
                    // is only used when no policy id was explicitly mentioned (e.g. with just [Authorize]).
                    var baselinePolicy = new AuthorizationPolicyBuilder()
                        // Ensure to enforce this on both authentication schemes.
                        .AddAuthenticationSchemes(b2cAuthenticationScheme, aadAuthenticationScheme)
                        // An authenticated user (i.e. an incoming JWT bearer token) is always required.
                        .RequireAuthenticatedUser()
                        // A "scope" or "roles" claim is also required, if not any application could simply request
                        // a valid access token to call into this API without being authorized.
                        .RequireAssertion(context =>
                        {
                            return context.User.Claims.Any(c => c.Type == Constants.ClaimTypes.Scope || c.Type == Constants.ClaimTypes.Roles);
                        })
                        .Build();
                    options.Filters.Add(new AuthorizeFilter(baselinePolicy));
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
