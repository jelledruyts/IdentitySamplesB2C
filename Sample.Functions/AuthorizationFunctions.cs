using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;

namespace Sample.Functions
{
    public static class AuthorizationFunctions
    {
        [FunctionName(nameof(AddAuthorizationClaims))]
        public static async Task<IActionResult> AddAuthorizationClaims(
                   [HttpTrigger(AuthorizationLevel.Function, WebRequestMethods.Http.Post)] HttpRequest request,
                   ILogger log)
        {
            try
            {
                log.LogInformation("Authorization claims are being requested.");

                // Parse and log the incoming request.
                var requestDocument = await ApiConnectorHelper.GetRequestJsonAsync(request, log);

                var step = requestDocument.RootElement.GetProperty("step").GetString();
                var objectId = requestDocument.RootElement.GetProperty("objectId").GetString();
                var clientId = requestDocument.RootElement.GetProperty("client_id").GetString();

                // Retrieve the app roles assigned to the user for the requested client application.
                var appRoles = await GetAppRolesAsync(objectId, clientId);

                // Custom user attributes cannot be arrays, so we emit them into a single claim value
                // separated with spaces (Azure AD App Roles cannot contain spaces).
                var appRolesValue = string.Join(' ', appRoles);

                return ApiConnectorHelper.GetContinueApiResponse("AddAuthorizationClaims-Succeeded", "Success", null, appRolesValue);
            }
            catch (Exception exc)
            {
                log.LogError(exc, "Error while processing request body: " + exc.ToString());
                return ApiConnectorHelper.GetBlockPageApiResponse("AddAuthorizationClaims-InternalError", "An error occurred while validating your invitation code, please try again later.");
            }
        }

        public static async Task<ICollection<string>> GetAppRolesAsync(string userId, string resourceAppId)
        {
            // Get configuration values as environment variables; for local development, ensure these are populated in local.settings.json.
            var tenantId = Environment.GetEnvironmentVariable("GraphTenantId", EnvironmentVariableTarget.Process);
            var clientId =  Environment.GetEnvironmentVariable("GraphClientId", EnvironmentVariableTarget.Process);
            var clientSecret =  Environment.GetEnvironmentVariable("GraphClientSecret", EnvironmentVariableTarget.Process);

            // Look up the user's app roles on the requested resource app.
            // This code requires (Application.Read.All + User.Read.All) OR (Directory.Read.All) for the
            // client application calling the Graph API.
            // In production code, the graph client as well as potentially the service principals of resource apps and perhaps
            // event the user's app roles for each resource app should be cached for optimized performance to avoid additional
            // requests for each individual user authentication.
            var confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithTenantId(tenantId)
                .WithClientSecret(clientSecret)
                .Build();
            var authProvider = new ClientCredentialProvider(confidentialClientApplication);
            var graphClient = new GraphServiceClient(authProvider);

            // Get the service principal of the resource app that the user is trying to sign in to.
            // See https://docs.microsoft.com/en-us/graph/api/serviceprincipal-list.
            var servicePrincipalsForResourceApp = await graphClient.ServicePrincipals.Request().Filter($"appId eq '{resourceAppId}'").GetAsync();
            var servicePrincipalForResourceApp = servicePrincipalsForResourceApp.Single();

            // Get all app role assignments for the given user and resource app service principal.
            // See https://docs.microsoft.com/en-us/graph/api/user-list-approleassignments.
            var userAppRoleAssignments = await graphClient.Users[userId].AppRoleAssignments.Request().Filter($"resourceId eq {servicePrincipalForResourceApp.Id}").GetAsync();
            var appRoleIds = userAppRoleAssignments.Select(a => a.AppRoleId).ToArray();
            var appRoles = servicePrincipalForResourceApp.AppRoles.Where(a => appRoleIds.Contains(a.Id)).Select(a => a.Value).ToArray();
            return appRoles;
        }
    }
}