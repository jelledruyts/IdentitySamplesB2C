using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;

namespace Sample.Client.ConsoleNetCore
{
    class Program
    {
        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        static void Main(string[] args)
        {
            RunAsync(args).Wait();
        }

        private static async Task RunAsync(string[] args)
        {
            // Load configuration.
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddUserSecrets<Program>(optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
            var sampleApiRootUrl = configuration.GetValue<string>("SampleApiRootUrl");
            var sampleApiScope = configuration.GetValue<string>("AzureAdB2C:Scope");
            var b2cAuthority = configuration.GetValue<string>("AzureAdB2C:Authority");
            var confidentialClientApplicationOptions = new ConfidentialClientApplicationOptions();
            configuration.Bind("AzureAdB2C", confidentialClientApplicationOptions);
            var scopes = new[] { sampleApiScope }; // The client credentials flow ALWAYS uses the "/.default" scope.

            while (true)
            {
                try
                {
                    Console.WriteLine("A - Call API using a client secret");
                    Console.Write("Type your choice and press Enter: ");
                    var choice = Console.ReadLine();
                    if (string.Equals(choice, "A", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Retrieve an access token to call the back-end Web API using a client secret
                        // representing this application (rather than a user).
                        var confidentialClientApplication = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(confidentialClientApplicationOptions)
                            .WithB2CAuthority(b2cAuthority)
                            .Build();
                        var token = await confidentialClientApplication.AcquireTokenForClient(scopes).ExecuteAsync();

                        // Put the access token on the authorization header by default.
                        var client = new HttpClient();
                        client.BaseAddress = new Uri(sampleApiRootUrl);
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

                        // Call the back-end Web API using the bearer access token.
                        var response = await client.GetAsync("api/identity");
                        response.EnsureSuccessStatusCode();

                        // Deserialize the response into an IdentityInfo instance.
                        var apiResponse = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Web API call was successful, raw response:");
                        Console.WriteLine(apiResponse);
                    }
                    else
                    {
                        break;
                    }
                    Console.WriteLine();
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.ToString());
                }
            }
        }
    }
}