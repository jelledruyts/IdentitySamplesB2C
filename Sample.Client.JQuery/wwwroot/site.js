"use strict";

var Sample = (function ($) {
    var appConfig = {
        scopes: [
            "https://identitysamplesb2c.onmicrosoft.com/sample-api/user_impersonation",
            "https://identitysamplesb2c.onmicrosoft.com/sample-api/Identity.Read",
            "https://identitysamplesb2c.onmicrosoft.com/sample-api/Identity.ReadWrite"
        ],
        sampleApiRootUrl: "https://localhost:5003/"
    };
    var msalConfig = {
        auth: {
            clientId: "21f0f8bf-c9fa-441f-bc80-020ddf4f7c15",
            authority: "https://identitysamplesb2c.b2clogin.com/tfp/identitysamplesb2c.onmicrosoft.com/B2C_1_Sample_Client_SignUpOrIn/v2.0/",
            validateAuthority: false
        },
        cache: {
            cacheLocation: "localStorage",
            storeAuthStateInCookie: true
        }
    };
    var clientApplication = new Msal.UserAgentApplication(msalConfig);

    var ensureSignedIn = function () {
        if (!clientApplication.getAccount()) {
            clientApplication.loginPopup({ scopes: appConfig.scopes })
                .then(function (loginResponse) {
                    updateUI();
                }).catch(function (error) {
                    alert("Could not sign in: " + error);
                });
        }
    };

    var updateUI = function () {
        var account = clientApplication.getAccount();
        if (account) {
            $("#signInLink").hide();
            $("#userNameText").html("Hello " + account.name + "!");
            $("#userNameText").show();
            $("#signOutLink").show();
            $("#identityInfoPanel").show();
        } else {
            $("#signInLink").show();
            $("#userNameText").hide();
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

        clientApplication.acquireTokenSilent({ scopes: appConfig.scopes }).then(function (accessTokenResponse) {
            getIdentityInfoFromWebApi(accessTokenResponse.accessToken);
        }).catch(function (error) {
            if (error.name === "InteractionRequiredAuthError") {
                clientApplication.acquireTokenPopup({ scopes: appConfig.scopes }).then(function (accessTokenResponse) {
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