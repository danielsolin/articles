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
            var urls = new[]
            {
                "https://example.com/api/getdata1",
                "https://example.com/api/getdata2",
                "https://example.com/api/getdata3"
            };

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
                    .ParseAdd("Plugin-Agent/1.0");

                try
                {
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

                    var resultJson = response.Content
                        .ReadAsStringAsync().GetAwaiter().GetResult();

                    var resultObj = JsonConvert.DeserializeObject<
                        Dictionary<string, object>>(resultJson);

                    if (resultObj.TryGetValue("results", out var rawResults) &&
                        rawResults is JArray jResults)
                    {
                        var results = jResults.Select(x => x.ToString())
                            .ToArray();
                        // Process results as needed
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