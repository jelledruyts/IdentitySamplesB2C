"use strict";

var Sample = (function ($) {
    var appConfig = {
        // Note: the scopes below are cloned before passing them along to MSAL (using the "slice" function)
        // because MSAL will append the "openid" and "profile" scopes to the array when signing in.
        // When then requesting a token with the same scopes array, these additional scopes will result in a token
        // cache lookup failing and a full request being done against AAD B2C to get a new token every time.
        scopes: [
            "https://identitysamplesb2c.onmicrosoft.com/sample-api/user_impersonation",
            "https://identitysamplesb2c.onmicrosoft.com/sample-api/Identity.Read",
            "https://identitysamplesb2c.onmicrosoft.com/sample-api/Identity.ReadWrite"
        ],
        sampleApiRootUrl: "https://localhost:5003/",
        signUpSignInPolicyId: "B2C_1_Sample_Client_SignUpOrIn",
        editProfilePolicyId: "B2C_1_Sample_Client_EditProfile",
    };
    var msalConfig = {
        auth: {
            clientId: "21f0f8bf-c9fa-441f-bc80-020ddf4f7c15",
            authority: "https://identitysamplesb2c.b2clogin.com/tfp/identitysamplesb2c.onmicrosoft.com/B2C_1_Sample_Client_SignUpOrIn/v2.0/",
            validateAuthority: false
        },
        system: {
            logger: new Msal.Logger(
                function loggerCallback(logLevel, message, containsPii) {
                    console.log("[MSAL] " + message);
                }, {
                    level: Msal.LogLevel.Verbose,
                    piiLoggingEnabled: true
                }
            )
        },
        cache: {
            cacheLocation: "localStorage",
            storeAuthStateInCookie: true
        }
    };
    var clientApplication = new Msal.UserAgentApplication(msalConfig);

    // Keep a global reference to the account as seen after the initial login, so it can
    // be passed to the acquireTokenSilent function for proper account lookup.
    // This is a workaround for the fact that when performing an additional user flow (B2C policy)
    // such as a profile editing flow after initial sign-in, MSAL will not correctly retrieve
    // the access token anymore due to an account identifier mismatch.
    // In fact, upon returning from B2C, MSAL will construct a new home account identifier
    // ("account.homeAccountIdentifier") based on the "client_info" parameter returned from B2C,
    // and in B2C the "client_info.uid" component includes the policy id that was just executed.
    // Therefore, the new account object seen by MSAL will have a different homeAccountIdentifier
    // than the one that was used to construct the local cache, and any access tokens are not
    // properly returned.
    var globalAccount = null;

    var performSignIn = function (policyId) {
        clientApplication.loginPopup({ scopes: appConfig.scopes.slice(0), extraQueryParameters: { p: policyId } })
            .then(function (loginResponse) {
                updateUI();
            }).catch(function (error) {
                alert("Could not sign in: " + error);
            });
    }

    var ensureSignedIn = function () {
        if (!clientApplication.getAccount()) {
            performSignIn(appConfig.signUpSignInPolicyId);
        }
    };

    var editProfile = function () {
        performSignIn(appConfig.editProfilePolicyId);
    };

    var updateUI = function () {
        var account = clientApplication.getAccount();
        if (globalAccount === null) {
            // Only set the global account reference once after initial sign-in (see above).
            globalAccount = account;
        }
        if (account) {
            $("#signInLink").hide();
            $("#editProfileLink").text("Hello " + account.name + "!");
            $("#editProfileLink").show();
            $("#signOutLink").show();
            $("#identityInfoPanel").show();
        } else {
            $("#signInLink").show();
            $("#userNameText").hide();
            $("#editProfileLink").hide();
            $("#signOutLink").hide();
            $("#identityInfoPanel").hide();
        }
    };

    var getIdentityInfo = function () {
        $("#identityInfoText").text("Loading...");
        $("#identityInfoText").show();

        var getIdentityInfoFromWebApi = function (accessToken) {
            $.support.cors = true;
            $.ajax({
                type: "GET",
                url: appConfig.sampleApiRootUrl + "api/identity",
                crossDomain: true,
                headers: {
                    "Authorization": "Bearer " + accessToken,
                },
            }).done(function (data) {
                $("#identityInfoText").text(JSON.stringify(data, null, 2));
                $("#identityInfoText").show();
            }).fail(function (jqXHR, textStatus) {
                $("#identityInfoText").text("Could not get identity info: " + textStatus);
            })
        };

        ensureSignedIn();

        // Pass in the (original) global account, not the "current" account in MSAL as this may have
        // an incorrect "homeAccountIdentifier" resulting in the access token becoming null (see above).
        clientApplication.acquireTokenSilent({ scopes: appConfig.scopes.slice(0), account: globalAccount }).then(function (accessTokenResponse) {
            getIdentityInfoFromWebApi(accessTokenResponse.accessToken);
        }).catch(function (error) {
            if (error.name === "InteractionRequiredAuthError") {
                clientApplication.acquireTokenPopup({ scopes: appConfig.scopes.slice(0) }).then(function (accessTokenResponse) {
                    getIdentityInfoFromWebApi(accessTokenResponse.accessToken);
                }).catch(function (error) {
                    alert("Could not acquire token: " + error);
                });
            }
            alert("Could not acquire token: " + error);
        });
    };

    var init = function () {
        $("#signInLink").on("click", function (event) {
            event.preventDefault();
            ensureSignedIn();
        });
        $("#editProfileLink").on("click", function (event) {
            event.preventDefault();
            editProfile();
        });
        $("#signOutLink").on("click", function (event) {
            event.preventDefault();
            clientApplication.logout();
        });
        $("#getIdentityInfoButton").on("click", function (event) {
            event.preventDefault();
            getIdentityInfo();
        });
        updateUI();
    };

    return {
        init: init
    };
})(jQuery);

$(document).ready(function () {
    Sample.init();
});