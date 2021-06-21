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
    public static class DebugFunctions
    {
        [FunctionName(nameof(Debug))]
        public static async Task<IActionResult> Debug(
                   [HttpTrigger(AuthorizationLevel.Function, WebRequestMethods.Http.Post)] HttpRequest request,
                   ILogger log)
        {
            try
            {
                log.LogInformation("A debug request was received.");

                // Parse and log the incoming request.
                await ApiConnectorHelper.GetRequestJsonAsync(request, log);

                // Return a "Continue" API response without modifying any of the claims.
                return ApiConnectorHelper.GetContinueApiResponse("Debug-Succeeded", "Success");
            }
            catch (Exception exc)
            {
                log.LogError(exc, "Error while processing request body: " + exc.ToString());
                return ApiConnectorHelper.GetBlockPageApiResponse("Debug-InternalError", "An error occurred while validating your invitation code, please try again later.");
            }
        }
    }
}