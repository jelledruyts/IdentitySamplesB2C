using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Sample.Functions
{
    public static class AuthorizationFunctions
    {
        [FunctionName(nameof(AddAuthorizationClaims))]
        public static async Task<IActionResult> AddAuthorizationClaims(
                   [HttpTrigger(AuthorizationLevel.Function, WebRequestMethods.Http.Post)] HttpRequest request,
                   IBinder binder,
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

                var appRoles = "Dummy app roles issued at " + DateTimeOffset.UtcNow.ToString(); // TODO

                return ApiConnectorHelper.GetContinueApiResponse("AddAuthorizationClaims-Succeeded", "Success", null, appRoles);
            }
            catch (Exception exc)
            {
                log.LogError(exc, "Error while processing request body: " + exc.ToString());
                return ApiConnectorHelper.GetBlockPageApiResponse("AddAuthorizationClaims-InternalError", "An error occurred while validating your invitation code, please try again later.");
            }
        }
    }
}