# Asynchronous Synchronous Plugins

## Problem: The Synchronous Wall in Dynamics 365 Plugins

When developing plugins that interact with external APIs, you quickly run into
a major limitation: **the Dataverse plugin sandbox does not reliably support
asynchronous execution**. While it's technically possible to use `async/await`
or `Task.Run`, doing so within the sandbox is risky and unsupported. These
approaches may appear to work in development or isolated cases, but they often
result in unpredictable behavior — such as deadlocks, thread aborts, or
context corruption — especially under load.

Because all plugin code must be **synchronous** and complete within strict
execution time limits, scenarios that would benefit from parallelism — like
making multiple HTTP calls simultaneously — become difficult or unsafe to
implement directly in the plugin.

## Solution: Async Work via Azure Functions

To work around this limitation, we can use an **Azure Function as an
asynchronous sidecar**. This function handles concurrent tasks (like multiple
API calls), while the plugin remains completely synchronous. The plugin sends a
payload to the function, blocks synchronously for the result, and then continues
with normal logic.

> ⚠️ Although the plugin **blocks** while waiting for the Azure Function, the
> function itself performs the async fan-out and waits for all tasks to complete
> — thus delivering parallelism without violating plugin constraints.

---

## Plugin Code (SyncPlugin.cs)

The plugin gathers a list of URLs, serializes them to JSON, sends them to the
Azure Function, and synchronously waits for the combined result.

```csharp
public void Execute(IServiceProvider serviceProvider)
{
    var urls = new[]
    {
        "https://example.com/api/getdata1",
        "https://example.com/api/getdata2",
        "https://example.com/api/getdata3"
    };

    var payload = new { urls };
    var json = JsonConvert.SerializeObject(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    using (var client = new HttpClient())
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DS-Agent/1.0");

        try
        {
            var response = client.PostAsync("http://localhost:7071/api/FanOut",
                content).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidPluginExecutionException(
                    $"Function call failed with status code: {response.StatusCode}");
            }

            var resultJson = response.Content.ReadAsStringAsync()
                .GetAwaiter().GetResult();

            var resultObj = JsonConvert.DeserializeObject<Dictionary<string,
                object>>(resultJson);

            if (resultObj.TryGetValue("results", out var rawResults) &&
                rawResults is JArray jResults)
            {
                var results = jResults.Select(x => x.ToString()).ToArray();
                foreach (var result in results)
                {
                    Console.WriteLine(result); // Replace with real logic
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidPluginExecutionException($"An error occurred:
                {ex.Message}", ex);
        }
    }
}
```

---

## Azure Function Code (AsyncFunction.cs)

This function receives a list of URLs, makes concurrent HTTP requests to them,
and returns the combined results as JSON.

```csharp
[Function("AsyncFunction")]
public async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
{
    _logger.LogInformation("Processing request in AsyncFunction.");

    string body = await new StreamReader(req.Body).ReadToEndAsync();
    JObject parsed = JObject.Parse(body);
    var urls = parsed["urls"]?.ToObject<string[]>() ?? Array.Empty<string>();

    var tasks = urls.Select(async url =>
    {
        try
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DS-Agent/1.0");
            return await _httpClient.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to fetch data from URL: {url}");
            return $"Error fetching data from {url}: {ex.Message}";
        }
    });

    string[] results = await Task.WhenAll(tasks);

    return new OkObjectResult(new { results });
}
```

---

## Example Input

```json
{
  "urls": [
    "https://example.com/api/getdata1",
    "https://example.com/api/getdata2",
    "https://example.com/api/getdata3"
  ]
}
```

## Example Output

```json
{
  "results": [
    "{...data from getdata1...}",
    "{...data from getdata2...}",
    "{...data from getdata3...}"
  ]
}
```

---

## Best Practices

- **Never call external services directly from the plugin** unless you’re sure
  they are fast and deterministic.
- **Use HttpClient responsibly** — don’t instantiate it per request in the Azure
  Function.
- **Handle all errors explicitly** and log them.
- **Validate the incoming payload carefully**.
- **Use dependency injection** in your Azure Function for logging and
  `HttpClient` reuse.

---

## Summary

Yes — it’s possible to get asynchronous behavior inside a synchronous Dataverse
plugin. You just need to **cheat a little**, by offloading the async work to
something that’s allowed to `await`. In this case, that something is an Azure
Function running in parallel and returning its result synchronously to the
plugin.

This is not only a clever hack — it’s often a real-world necessity.

---

> 🧪 Use this responsibly. You’re still within the plugin timeout window and
> relying on external availability.