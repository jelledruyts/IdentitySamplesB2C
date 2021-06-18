using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Sample.Functions
{
    // An invitation code request can contain any pre-identified information of the user
    // that should be copied into their user account once they actually sign up using
    // the provided invitation code; in this example we use a "Company ID" but it could
    // also be a customer loyalty number, the role of the user in the application they are
    // signing up to, an internal identifier of the user in another existing system, ...
    public class InvitationCodeRequest
    {
        public string CompanyId { get; set; }
    }

    public class InvitationCodeResponse
    {
        public InvitationCodeRequest Request { get; set; }
        public string InvitationCode { get; set; }
    }

    public static class UserInvitationFunctions
    {

        [FunctionName(nameof(CreateUserInvitation))]
        public static async Task<InvitationCodeResponse> CreateUserInvitation(
            [HttpTrigger(AuthorizationLevel.Function, WebRequestMethods.Http.Post)] InvitationCodeRequest request,
            IBinder binder,
            ILogger log)
        {
            log.LogInformation("A new user invitation was requested.");

            // Create a new invitation code that cannot easily be guessed by others and is unique so that it cannot
            // conflict with an invitation code for another user (as at this point the invitation code already represents
            // the user-to-be).
            var invitationCode = Guid.NewGuid().ToString();

            // Write the user invitation request to persistent storage in a way that it can easily be retrieved using
            // just the invitation code.
            using (var blobOutputStream = binder.Bind<Stream>(new BlobAttribute($"userinvitations/{invitationCode}.json", FileAccess.Write)))
            {
                await JsonSerializer.SerializeAsync(blobOutputStream, request);
            };
            log.LogInformation($"User invitation was stored in persistent storage with invitation code \"{invitationCode}\".");

            // Return the invitation code for the user back to the caller (along with the original request).
            return new InvitationCodeResponse { Request = request, InvitationCode = invitationCode };
        }

        [FunctionName(nameof(RedeemInvitationCode))]
        public static async Task<IActionResult> RedeemInvitationCode(
            [HttpTrigger(AuthorizationLevel.Function, WebRequestMethods.Http.Post)] HttpRequest request,
            IBinder binder,
            ILogger log)
        {
            // Azure AD B2C calls into this API when a user is attempting to sign up with an invitation code.
            // We expect a JSON object in the HTTP request which contains the input claims as well as an additional
            // property "ui_locales" containing the locale being used in the user journey (browser flow).
            try
            {
                log.LogInformation("An invitation code is being redeemed.");

                // Parse and log the incoming request.
                var requestDocument = await ApiConnectorHelper.GetRequestJsonAsync(request, log);

                // Look up the invitation code in the incoming request. The element name depends on the
                // extension App ID (e.g. "extension_bd88c9da63214d09b854af9cfbbf4b15_InvitationCode")
                // so to avoid hard-coding this or making it configurable, we just check if the element
                // name ends with the custom attribute name.
                var invitationCode = default(string);
                foreach (var element in requestDocument.RootElement.EnumerateObject())
                {
                    if (element.Name.EndsWith("InvitationCode", StringComparison.InvariantCultureIgnoreCase)) // E.g. "extension_bd88c9da63214d09b854af9cfbbf4b15_InvitationCode"
                    {
                        invitationCode = element.Value.GetString();
                    }
                }

                if (string.IsNullOrWhiteSpace(invitationCode) || invitationCode.Length < 10)
                {
                    // No invitation code was found in the request or it was too short, return a validation error.
                    log.LogInformation($"The provided invitation code \"{invitationCode}\" is invalid.");
                    return ApiConnectorHelper.GetValidationErrorApiResponse("UserInvitationRedemptionFailed-Invalid", "The invitation code you provided is invalid.");
                }
                else
                {
                    // An invitation code was found in the request, look up the user invitation in persistent storage.
                    log.LogInformation($"Looking up user invitation for invitation code \"{invitationCode}\"...");
                    var blobInputStream = binder.Bind<Stream>(new BlobAttribute($"userinvitations/{invitationCode}.json", FileAccess.Read));
                    if (blobInputStream == null)
                    {
                        // The requested invitation code was not found in persistent storage.
                        log.LogWarning($"User invitation for invitation code \"{invitationCode}\" was not found.");
                        return ApiConnectorHelper.GetValidationErrorApiResponse("UserInvitationRedemptionFailed-NotFound", "The invitation code you provided is invalid.");
                    }
                    else
                    {
                        // The requested invitation code was found in persistent storage, look up the pre-identified information.
                        log.LogInformation($"User invitation found for invitation code \"{invitationCode}\".");
                        var invitationCodeRequest = await JsonSerializer.DeserializeAsync<InvitationCodeRequest>(blobInputStream);

                        // TODO: At this point, the blob can be deleted again.

                        return ApiConnectorHelper.GetContinueApiResponse("UserInvitationRedemptionSucceeded", "The invitation code you provided is valid.", invitationCodeRequest.CompanyId);
                    }
                }
            }
            catch (Exception exc)
            {
                log.LogError(exc, "Error while processing request body: " + exc.ToString());
                return ApiConnectorHelper.GetBlockPageApiResponse("UserInvitationRedemptionFailed-InternalError", "An error occurred while validating your invitation code, please try again later.");
            }
        }
    }
}