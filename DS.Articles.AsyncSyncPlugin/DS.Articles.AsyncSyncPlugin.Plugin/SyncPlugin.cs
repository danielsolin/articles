using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace DS.Articles.AsyncSyncPlugin.Plugin
{
    public class SyncPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Define the URLs to be processed by the Azure Function
            var urls = new[]
            {
                "https://example.com/api/getdata1",
                "https://example.com/api/getdata2",
                "https://example.com/api/getdata3"
            };

            // Prepare the payload to send to the Azure Function
            var payload = new
            {
                urls
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(
                json, Encoding.UTF8, "application/json");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent
                    .ParseAdd("DS-Agent/1.0");

                try
                {
                    // Send the payload to the Azure Function
                    var response = client.PostAsync(
                        "http://localhost:7071/api/FanOut", content)
                        .GetAwaiter().GetResult();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidPluginExecutionException(
                            $"Function call failed with status code: " +
                            $"{response.StatusCode}"
                        );
                    }

                    // Read and parse the response from the Azure Function
                    var resultJson = response.Content
                        .ReadAsStringAsync().GetAwaiter().GetResult();

                    var resultObj = JsonConvert.DeserializeObject<
                        Dictionary<string, object>>(resultJson);

                    if (resultObj.TryGetValue("results", out var rawResults) &&
                        rawResults is JArray jResults)
                    {
                        var results = jResults.Select(x => x.ToString())
                            .ToArray();

                        // Log or process the results as needed
                        foreach (var result in results)
                        {
                            // Example: Log each result (replace with actual logic)
                            Console.WriteLine(result);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(
                        $"An error occurred: {ex.Message}", ex);
                }
            }
        }
    }
}