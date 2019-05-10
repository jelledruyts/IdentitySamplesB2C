# Identity Samples for Azure AD B2C

This repository contains a Visual Studio (Code) solution that demonstrates modern claims-based identity scenarios for .NET developers, with a particular focus on authentication and authorization using [Azure Active Directory B2C](https://azure.microsoft.com/en-us/services/active-directory-b2c/).

**IMPORTANT NOTE: The code in this repository is _not_ production-ready. It serves only to demonstrate the main points via minimal working code, and contains no exception handling or other special cases. Refer to the official documentation and samples for more information. Similarly, by design, it does not implement any caching or data persistence (e.g. to a database) to minimize the concepts and technologies being used.**

## Projects

The solution consists of the following parts:

- `Sample.Client.AspNetCore22`
  - This is a server-side ASP.NET Core 2.2 web application that users can sign into (using OpenID Connect) and which calls into a secured Web API (using OAuth 2.0 bearer tokens)
- `Sample.Api.AspNetCore22`
  - This is an ASP.NET Core 2.2 Web API which is protected by OAuth 2.0 bearer tokens
- `CustomPolicies`
  - This folder contains custom policies for Azure AD B2C

## Scenarios

The following scenarios are showcased:

- [Web App Sign-In (ASP.NET Core)](#web-app-sign-in-aspnet-core)
- [Web App calling Web API](#web-app-calling-web-api)
- [Web App Sign-In + Web API (JavaScript)](#web-app-sign-in--web-api-javascript)
- [User invitation using custom policy](#user-invitation-using-custom-policy)

### Web App Sign-In (ASP.NET Core)

This scenario allows users to sign in to an ASP.NET web application using a "user flow" (policy) in Azure AD B2C.

To set this up locally, ensure you have performed the following steps:

- Register an application in Azure AD B2C to represent the web application
  - Use `https://localhost:5001/signin-oidc` as the Reply URL
  - Ensure to allow the implicit flow
  - Create a client secret (app key)
- Create a user flow (policy) for a combined "sign up or in" experience
- Provide the relevant app settings to the application

Here are the relevant code fragments:

- [Startup.cs (48-49)](Sample.Client.AspNetCore22/Startup.cs#L48-L49): use Azure AD B2C for authentication (using OpenID Connect)
- [appsettings.json (2-10)](Sample.Client.AspNetCore22/appsettings.json#L2-L10): define the settings needed for the Azure AD B2C middleware to work (e.g. the tenant instance, Client ID and policies to use)

### Web App calling Web API

This scenario demonstrates that the ASP.NET web application can perform a server-side call to an external Web API _on behalf of the end user_ (i.e. with "delegated permissions"). This uses a hybrid OpenID Connect flow which not only returns an ID token (to identify the user to the web application) but also an authorization code which is then exchanged for an access token to present to the back-end Web API.

To set this up locally, ensure you have performed the following steps:

- Register an application in Azure AD B2C to represent the Web API
  - Use `https://localhost:5003` as the Reply URL
  - Register an App ID URI in order to expose scopes
  - Publish two scopes, one for read (e.g. `Identity.Read`) and one for write (e.g. `Identity.ReadWrite`)
- On the client application, specify the API access to the Web API (select the default `user_impersonation` scope as well as the two other scopes you created)
- Provide the relevant app settings to both applications (Web App and Web API)

Here are the relevant code fragments on the client side (the ASP.NET Web App):

- [Startup.cs (57)](Sample.Client.AspNetCore22/Startup.cs#L57): during the OpenID Connect sign-in, trigger a hybrid flow to request not only the ID token (which is the default) but also an authorization code
- [Startup.cs (59)](Sample.Client.AspNetCore22/Startup.cs#L59): also request a refresh token to be able to renew the access token without having to prompt the user again
- [Startup.cs (65-67)](Sample.Client.AspNetCore22/Startup.cs#L65-67): define the scopes (and thereby implicitly the API) for which the access token is requested (by redeeming it from the authorization code)
- [Startup.cs (127-129)](Sample.Client.AspNetCore22/Startup.cs#L127-L129): when the authorization code has been redeemed for the access token, it would typically get stored in a cache (e.g. an [MSAL.NET token cache](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/Acquiring-tokens-with-authorization-codes-on-web-apps)); here for simplicity we'll just add it to the user claims so it is serialized as part of the authentication cookie and can be easily retrieved later
- [AccountController.cs (45)](Sample.Client.AspNetCore22/Controllers/AccountController.cs#L45): retrieve the access token for the back-end Web API from the user's claims (which were automatically deserialized from the authentication cookie where they were stored in the previous step)
- [AccountController.cs (49)](Sample.Client.AspNetCore22/Controllers/AccountController.cs#L49): send the access token as a "bearer" token to the back-end Web API

Here are the relevant code fragments on the server side (the Web API):

- [Startup.cs (38-39)](Sample.Api.AspNetCore22/Startup.cs#L38-39): use OAuth 2.0 bearer tokens for authorization
- [Startup.cs (41)](Sample.Api.AspNetCore22/Startup.cs#L41): define the authority, which allows the middleware to retrieve all details about the issuer (e.g. the signing keys to validate the token signature)
- [Startup.cs (42)](Sample.Api.AspNetCore22/Startup.cs#L42): define the audience to ensure incoming tokens are only accepted if they are truly intended for _this_ application
- [Startup.cs (59)](Sample.Api.AspNetCore22/Startup.cs#L59): define authorization rules so that the API can be secured based on the incoming token
- [Startup.cs (61-68)](Sample.Api.AspNetCore22/Startup.cs#L61-L68): define a baseline authorization policy that requires at least an authenticated user (i.e. calls without a valid access token will be rejected)
- [Startup.cs (85)](Sample.Api.AspNetCore22/Startup.cs#L95): apply the baseline authorization policy to _all_ requests
- [Startup.cs (69-77)](Sample.Api.AspNetCore22/Startup.cs#L69-L77): define a `ReadIdentity` authorization policy that requires a scope claim for the configured "read" permission
- [IdentityController.cs (9)](Sample.Api.AspNetCore22/Controllers/IdentityController.cs#L9): require that this controller can only be called when it satisfies the `ReadIdentity` authorization policy defined above (i.e. when it has "read" permissions on the identity resource)
- [IdentityController.cs (25)](Sample.Api.AspNetCore22/Controllers/IdentityController.cs#L25): access the claims in the token directly from the `User` object (which was populated automatically by the authentication middleware)

### Web App Sign-In + Web API (JavaScript)

This scenario allows users to sign in to a client-side web application using a "user flow" (policy) in Azure AD B2C and call the same Web API from JavaScript in the browser.

To set this up locally, ensure you have performed the following steps:

- Register an application in Azure AD B2C to represent the web application
  - Use `https://localhost:5005` as the Reply URL
  - Ensure to allow the implicit flow
- On the client application, specify the API access to the Web API (select the default `user_impersonation` scope as well as the two other scopes you created)
- Provide the relevant app settings to the application

Here are the relevant code fragments:

- [site.js (4-22)](Sample.Client.JQuery/wwwroot/site.js#L4-L22): define the relevant configuration settings
- [site.js (23)](Sample.Client.JQuery/wwwroot/site.js#L23): use [MSAL.js](https://github.com/AzureAD/microsoft-authentication-library-for-js) to represent the user agent application
- [site.js (27)](Sample.Client.JQuery/wwwroot/site.js#L27): sign the user in using a popup
- [site.js (75-87)](Sample.Client.JQuery/wwwroot/site.js#L75-L87): acquire an access token for the back-end Web API
- [site.js (63)](Sample.Client.JQuery/wwwroot/site.js#L63): send the access token as a "bearer" token to the back-end Web API

### User invitation using custom policy

This scenario demonstrates that you can use Azure AD B2C not only for traditional self-service sign-up of end users, but that you can also lock down the directory and application by only allowing users to sign up that you have explicitly invited.

> Note that this scenario is also covered in the very extensive [WingTip Games B2C sample solution](https://github.com/Azure-Samples/active-directory-b2c-advanced-policies/blob/master/wingtipgamesb2c/), especially check out the [invitation flow documentation](https://github.com/Azure-Samples/active-directory-b2c-advanced-policies/blob/master/wingtipgamesb2c/Implementing%20an%20invitation%20flow%2C%20Sample%20by%20Kloud.docx) for details. However, this sample solution and its custom policies cover _many_ scenarios which makes it difficult to focus only on this individual use case.

This is a brief summary of how this scenario works:

- The application has a page where you can generate an invitation link for a particular email address
- You can then send that link to the person you want to invite into the application (and therefore allow them to sign up to Azure AD B2C)
- The link allows the user to sign up and contains a (signed) piece of information which includes their email address, so that it cannot be intercepted and modified
  - OpenID Connect and OAuth 2.0 have a concept of [assertions](https://tools.ietf.org/html/rfc7521) in their flows to convey exactly this information about the user through the use of a `client_assertion` parameter
  - This client assertion can be encoded as a JWT token, signed with the application's client secret (or any other key), and then included as part of the authentication flow
- A custom policy in Azure AD B2C inspects the incoming client assertion JWT token, validates its signature and registers the user with the verified email address
- The user is then returned back to the application, with a valid registered account

There are two options for generating the invitation link:

- The application can generate a link that sends the user directly to Azure AD B2C
  - This link would be a regular OpenID Connect authorization URL including the Azure AD B2C custom policy as well as the client assertion
  - E.g. the invitation link could look like `https://yourtenant.b2clogin.com/yourtenant.onmicrosoft.com/oauth2/v2.0/authorize?p=b2c_1a_sample_client_invitation&client_assertion=<jwt>&...`
  - The challenge here is that ASP.NET Core expects a correlation cookie to prevent against Cross-Site Request Forgery (XSRF) attacks, and will fail if it's missing
  - Indeed as the user has not first visited the site before being authorized, that cookie will not be present and the sign-in will fail
  - The [WingTip Games B2C sample solution](https://github.com/Azure-Samples/active-directory-b2c-advanced-policies/blob/master/wingtipgamesb2c/) shows how this can be solved by using custom middleware that bypasses the correlation check for this specific flow (the "Policy Link" approach in the sample)
- The application can generate a link back to the application first, which then redirects to the Azure AD B2C custom policy (similar to a regular sign-in)
  - E.g. the invitation link could look like `https://www.example.com/account/register?client_assertion=<jwt>`
  - This means that no special middleware or configuration is needed, only the `client_assertion` needs to be set appropriately
  - This is the approach used in this sample (as well as in the "Application Link" approach in WingTip Games) as it's much easier to implement and allows you to still change the approach later on (since the user's initial entry point is still your own application)

To set this up locally, ensure you have performed the following steps:

- Follow the guide to [get started with custom policies in Azure AD B2C](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-get-started-custom)
  - The custom policies in this solution have been adapted from the `SocialAndLocalAccounts` folder in the "starter pack"
  - Add the signing and encryption keys as explained, including the Facebook key (because it is part of the `TrustFrameworkBase.xml` file in the `SocialAndLocalAccounts` starter pack; feel free to use placeholder key if you don't want to register a real Facebook app)
  - Add another signature key and set the value manually to the client secret of the client-side Web Application (as the `client_assertion` signature that the application generates will need to be validated against this key)
- (Optional) Follow the guide to [collect logs using Application Insights](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-troubleshoot-custom)
  - Replace the `InstrumentationKey` setting in the custom policies with your own Application Insights key

Here are the relevant code fragments on the application side:

- [AccountController.cs (95-101)](Sample.Client.AspNetCore22/Controllers/AccountController.cs#L95-L101): generate a self-issued JWT token which includes the `verified_email` claim, and is signed with the application's client secret
- [AccountController.cs (103-106)](Sample.Client.AspNetCore22/Controllers/AccountController.cs#L103-L106): generate an absolute link back to the application's account registration URL (including the client assertion)
- [AccountController.cs (118-125)](Sample.Client.AspNetCore22/Controllers/AccountController.cs#L118-L125): when the user follows the link, the application triggers a sign-in against Azure AD B2C with a specific custom policy for the user invitation, and passes along the `client_assertion` it received in the link
- [Startup.cs (92-98)](Sample.Client.AspNetCore22/Startup.cs#L92-L98): before redirecting to the identity provider (i.e. Azure AD B2C), the standard `client_assertion` and `client_assertion_type` request parameters are set so that they can be used from the custom policy

Here are the relevant custom policy fragments:

- [TrustFrameworkBase.xml](CustomPolicies/TrustFrameworkBase.xml): this is the base file from the starter pack; other than specifying the correct tenant, no changes were made to this file (which is recommended, so that any customizations are applied in the other files only)
- [TrustFrameworkExtensions.xml (18-29)](CustomPolicies/TrustFrameworkExtensions.xml#L18-L29): add a custom claim type for the `verified_email` claim; this is set to read-only to prevent users from altering this (which is also why we cannot use the built-in `email` claim type as a user would be allowed to edit it then when signing up)
- [TrustFrameworkExtensions.xml (32-43)](CustomPolicies/TrustFrameworkExtensions.xml#L32-L43): add a `CreateEmailFromVerifiedEmail` claims transformation which copies the custom verified email claim to the built-in email claim
- [TrustFrameworkExtensions.xml (73-108)](CustomPolicies/TrustFrameworkExtensions.xml#L73-L108): register a technical profile which will perform the registration of a new local account using the verified email address
- [TrustFrameworkExtensions.xml (114-125)](CustomPolicies/TrustFrameworkExtensions.xml#L114-L125): define the `Invitation` user journey, which only contains a single claims exchange step to register the local account (through the technical profile registered above), and then sends the claims back to the application
- [Sample_Client_SignUpOrSignIn.xml](CustomPolicies/Sample_Client_SignUpOrSignIn.xml): this is the default "sign-up or sign-in" policy from the starter pack, which can be used to sign in (see note below); it was only modified to include Application Insights and to remove unused output claims
- [Sample_Client_Invitation.xml (20)](CustomPolicies/Sample_Client_Invitation.xml#L20): reference the `Invitation` user journey defined in the `TrustFrameworkExtensions.xml` file
- [Sample_Client_Invitation.xml (28)](CustomPolicies/Sample_Client_Invitation.xml#L28): the input token format is set to `JWT` because we are accepting incoming claims from the `client_assertion` JWT token (which is passed as a query parameter to this policy)
- [Sample_Client_Invitation.xml (31)](CustomPolicies/Sample_Client_Invitation.xml#L31): reference the policy key name that represents the Web Application's client secret so that the `client_assertion` JWT token signature can be validated
- [Sample_Client_Invitation.xml (34)](CustomPolicies/Sample_Client_Invitation.xml#L34): extract the `verified_email` claim from the incoming `client_assertion` so that it can be converted to the built-in `email` claim by the claims transformation registered earlier

> Note that when using the custom policy to invite users, you will also need to perform a _regular_ sign-in with a custom policy. This is because the signing keys do not match between the built-in policy and the custom policy (for which you created separate keys). The ASP.NET middleware only retrieves the signing keys for the sign-in flow, which means the user's token that is returned after the invitation custom policy will not be considered valid. For that reason, the `Sample_Client_SignUpOrSignIn.xml` custom policy is also included here, which you can use to sign in, as it uses the same signing keys as the invitation policy. Make sure to update the application configuration to use the correct policies for this scenario.