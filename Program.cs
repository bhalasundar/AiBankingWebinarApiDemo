using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => "AI Banking Webinar API is running.");

app.MapPost("/api/ask", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    var request = JsonSerializer.Deserialize<QuestionRequest>(body, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (request == null || string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new { answer = "Question is required." });
    }

    var apiKey = builder.Configuration["OpenAI:ApiKey"];

    if (string.IsNullOrEmpty(apiKey))
    {
        return Results.Ok(new
        {
            answer = "OpenAI API key is missing."
        });
    }

    var systemPrompt = """
You are a professional banking assistant in the UAE.

Answer clearly and professionally.

If the question is about banking products, explain generally and say that final approval depends on the bank's policy.
""";

    var openAiRequest = new
    {
        model = "gpt-4o-mini",
        messages = new object[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = request.Question }
        },
        temperature = 0.3
    };

    var client = httpClientFactory.CreateClient();

    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", apiKey);

    var json = JsonSerializer.Serialize(openAiRequest);

    var response = await client.PostAsync(
        "https://api.openai.com/v1/chat/completions",
        new StringContent(json, Encoding.UTF8, "application/json"));

    var responseText = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
{
    return Results.Ok(new
    {
        answer = "OpenAI call failed.",
        statusCode = (int)response.StatusCode,
        details = responseText
    });
}

    using var doc = JsonDocument.Parse(responseText);

    var answer = doc.RootElement
        .GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString();

    return Results.Ok(new { answer });

});

app.Run();

public record QuestionRequest(string Question);