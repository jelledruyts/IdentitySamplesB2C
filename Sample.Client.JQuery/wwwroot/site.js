"use strict";

var Sample = (function ($) {
    var appConfig = {
        scopes: [
            "https://identitysamplesb2c.onmicrosoft.com/sample-api/user_impersonation",
            "https://identitysamplesb2c.onmicrosoft.com/sample-api/Identity.Read",
            "https://identitysamplesb2c.onmicrosoft.com/sample-api/Identity.ReadWrite"
        ],
        sampleApiRootUrl: "https://localhost:5003/",
        b2cTenantName: "identitysamplesb2c",
        b2cClientId: "21f0f8bf-c9fa-441f-bc80-020ddf4f7c15",
        signUpSignInPolicyId: "B2C_1_Sample_Client_SignUpOrIn",
        editProfilePolicyId: "B2C_1_Sample_Client_EditProfile",
        loginInteractionType: "popup", // Can be "popup" or "redirect"
        forceSignIn: false // Always triggers a sign-in (no explicit sign-in user action needed)
    };
    var msalConfig = {
        auth: {
            clientId: appConfig.b2cClientId,
            authority: `https://${appConfig.b2cTenantName}.b2clogin.com/${appConfig.b2cTenantName}.onmicrosoft.com/${appConfig.signUpSignInPolicyId}`,
            knownAuthorities: [`${appConfig.b2cTenantName}.b2clogin.com`]
        },
        system: {
            loggerOptions: {
                loggerCallback: function (level, message, containsPii) {
                    switch (level) {
                        case 0 /*LogLevel.Error*/:
                            console.error("[MSAL] " + message);
                            return;
                        case 2 /*LogLevel.Info*/:
                            console.info("[MSAL] " + message);
                            return;
                        case 3 /*LogLevel.Verbose*/:
                            console.debug("[MSAL] " + message);
                            return;
                        case 1 /*LogLevel.Warning*/:
                            console.warn("[MSAL] " + message);
                            return;
                    }
                },
                piiLoggingEnabled: true,
                logLevel: 3 /*LogLevel.Verbose*/
            }
        },
        cache: {
            cacheLocation: "localStorage",
            storeAuthStateInCookie: true
        }
    };
    var clientApplication = new msal.PublicClientApplication(msalConfig);

    var performSignIn = function (policyId) {
        var loginRequest = {
            authority: `https://${appConfig.b2cTenantName}.b2clogin.com/${appConfig.b2cTenantName}.onmicrosoft.com/${policyId}`,
            scopes: appConfig.scopes
        };
        if (appConfig.loginInteractionType === 'redirect') {
            clientApplication.loginRedirect(loginRequest)
                .catch(function (error) {
                    alert("Could not sign in: " + error);
                });
        } else {
            clientApplication.loginPopup(loginRequest)
                .then(function (loginResponse) {
                    updateUI();
                }).catch(function (error) {
                    alert("Could not sign in: " + error);
                });
        }
    }

    var getAccount = function () {
        var accounts = clientApplication.getAllAccounts();
        if (!accounts || accounts.length === 0) {
            return null;
        } else {
            return accounts[0];
        }
    }

    var ensureSignedIn = function () {
        if (!getAccount()) {
            performSignIn(appConfig.signUpSignInPolicyId);
        }
    };

    var editProfile = function () {
        performSignIn(appConfig.editProfilePolicyId);
    };

    var updateUI = function () {
        var account = getAccount();
        if (account) {
            $("#signInLink").hide();
            $("#editProfileLink").text("Hello " + account.username + "!");
            $("#editProfileLink").show();
            $("#signOutLink").show();
            $("#identityInfoPanel").show();
        } else {
            $("#signInLink").show();
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

        clientApplication.acquireTokenSilent({ scopes: appConfig.scopes, account: getAccount() }).then(function (accessTokenResponse) {
            getIdentityInfoFromWebApi(accessTokenResponse.accessToken);
        }).catch(function (error) {
            if (error.name === "InteractionRequiredAuthError") {
                clientApplication.acquireTokenPopup({ scopes: appConfig.scopes, account: getAccount() }).then(function (accessTokenResponse) {
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

        if (appConfig.loginInteractionType === 'redirect') {
            // Handle the redirect flows
            clientApplication.handleRedirectPromise().then((tokenResponse) => {
                if (tokenResponse === null) {
                    // Not coming back from an authentication request.
                    if (appConfig.forceSignIn) {
                        ensureSignedIn();
                    }
                } else {
                    // Coming back from an authentication request, update the UI.
                    updateUI();
                }
            }).catch((error) => {
                alert("Could not sign in: " + error);
            });
        } else {
            if (appConfig.forceSignIn) {
                ensureSignedIn();
            }
        }

        updateUI();
    };

    return {
        init: init
    };
})(jQuery);

$(document).ready(function () {
    Sample.init();
});