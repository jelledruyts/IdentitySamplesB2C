using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Sample.Functions
{
    public static class ApiConnectorHelper
    {
        public static async Task<JsonDocument> GetRequestJsonAsync(HttpRequest request, ILogger logger)
        {
            using (var reader = new StreamReader(request.Body))
            {
                var requestBody = await reader.ReadToEndAsync();
                var requestDocument = JsonDocument.Parse(requestBody);
                if (logger != null)
                {
                    logger.LogInformation("Request body:");
                    logger.LogInformation(JsonSerializer.Serialize(requestDocument.RootElement, new JsonSerializerOptions { WriteIndented = true }));
                }
                return requestDocument;
            }
        }

        public static IActionResult GetContinueApiResponse(string code, string userMessage, string companyId = null, string appRoles = null)
        {
            return GetApiResponse("Continue", code, userMessage, 200, companyId, appRoles);
        }

        public static IActionResult GetValidationErrorApiResponse(string code, string userMessage)
        {
            return GetApiResponse("ValidationError", code, userMessage, 400);
        }

        public static IActionResult GetBlockPageApiResponse(string code, string userMessage)
        {
            return GetApiResponse("ShowBlockPage", code, userMessage, 200);
        }

        public static IActionResult GetApiResponse(string action, string code, string userMessage, int statusCode, string companyId = null, string appRoles = null)
        {
            var responseProperties = new Dictionary<string, object>
            {
                { "version", "1.0.0" }, // For both
                { "status", statusCode }, // For both
                { "action", action }, // For API Connectors
                { "code", code }, // For IEF REST profile
                { "userMessage", userMessage } // For both
            };
            // Optionally return custom user attributes in simplified form (without the Extension App ID).
            if (companyId != null)
            {
                responseProperties["extension_CompanyId"] = companyId;
            }
            if (appRoles != null)
            {
                responseProperties["extension_AppRoles"] = appRoles;
            }
            return new JsonResult(responseProperties) { StatusCode = statusCode };
        }
    }
}