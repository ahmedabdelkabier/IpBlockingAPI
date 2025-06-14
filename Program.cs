using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using MinimalBlockingAPI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("ipLookup", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(context),
            factory: _ => new()
            {
                PermitLimit = 30, 
                Window = TimeSpan.FromMinutes(1)
            }));
});
builder.Services.AddHostedService<TemporalBlockRemovalService>(); 

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseRateLimiter();
app.UseHttpsRedirection();

// Add to Program.cs before other middleware
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Modified IP detection
string GetClientIp(HttpContext context)
{
    // Check headers first (cloudflare, railway, etc)
    if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var cloudflareIp))
        return cloudflareIp.ToString();

    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedIp))
        return forwardedIp.ToString().Split(',')[0].Trim();

    // Fallback to connection IP
    return context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
}

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant();

    // Exclude /check and /logs/blocked-attemps endpoints
    if (path == "/check" || path == "/logs/blocked-attemps")
    {
        await next();
        return;
    }

    var ip = GetClientIp(context);

    if (!string.IsNullOrEmpty(ip))
    {
        var countryCode = await CountriesCollection.getCountryCode(ip);
        if (CountriesCollection.countries.Any(c => c.Key.Code == countryCode))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync($"Access from {countryCode} is blocked");
            return;
        }
    }
    
    await next();
});


app.MapGet("/blocked", (string? search, int? pageNumber, int? pageSize) =>
{
    pageNumber ??= 1;
    pageSize ??= 10;

    if (pageNumber < 1) return Results.BadRequest("pageNumber must be 1 or greater.");
    if (pageSize < 1 || pageSize > 100) return Results.BadRequest("pageSize must be between 1 and 100.");

    var countries = CountriesCollection.countries.AsEnumerable();

    if (!string.IsNullOrEmpty(search))
    {
        countries = countries
            .Where(c => c.Key.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.Key.Code.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    var pagedCountries = countries
        .Skip((pageNumber.Value - 1) * pageSize.Value)
        .Take(pageSize.Value).Select(c => c.Key)
        .ToList();

    return Results.Ok(pagedCountries);
});

app.MapPost("/block", (Country country) =>
{
    if (CountriesCollection.countries.Any(c => c.Key.Code == country.Code))
    {
        return Results.BadRequest($"Country with code {country.Code} already exists.");
    }
    CountriesCollection.addNewBlockedCountry(country);
    return Results.Created();
});

app.MapDelete("/unblock/{code}", (string code) =>
{
    bool deleted = CountriesCollection.deleteBlockedCountry(code);
    if (deleted)
    {
        return Results.Ok($"Country with code {code} has been unblocked.");
    }
    else
    {
        return Results.NotFound($"Country with code {code} not found.");
    }
});

app.MapGet("/check", async (HttpContext context) =>
{
    var ip = GetClientIp(context);
    if (string.IsNullOrEmpty(ip))
    {
        return Results.BadRequest("Could not determine client IP address.");
    }
    var countryCode = await CountriesCollection.getCountryCode(ip);
    if (CountriesCollection.countries.Any(c => c.Key.Code == countryCode))
    {
        BlockedAttempsCollection.addNewBlockedAttempt(new BlockedAttempt(ip, countryCode, "Blocked"));
        return Results.Problem($"Access from {countryCode} is not allowed", statusCode: 403);
    }   
    return Results.Ok(new { CountryCode = countryCode });
});

app.MapGet("/lookup", async (string ip) =>
{
    if (!IPAddress.TryParse(ip, out _))
        return Results.BadRequest("Invalid IP address format");

    try
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0");

        var response = await client.GetAsync($"https://ipapi.co/{ip}/json/");

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return Results.Problem("API rate limit exceeded", statusCode: 429);

        response.EnsureSuccessStatusCode();

        return Results.Text(
            await response.Content.ReadAsStringAsync(),
            "application/json"
        );
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(
            title: "IP Lookup Failed",
            detail: ex.Message,
            statusCode: (int?)ex.StatusCode ?? 500
        );
    }
}).RequireRateLimiting("ipLookup");

app.MapGet("/logs/blocked-attemps", async (int? pageNumber, int? pageSize) =>
{
    pageNumber ??= 1;
    pageSize ??= 10;
    var blockedAttempts = BlockedAttempsCollection.blockedAttempts;

    if (pageNumber < 1) return Results.BadRequest("pageNumber must be 1 or greater.");

    if (pageSize < 1 || pageSize > 100) return Results.BadRequest("pageSize must be between 1 and 100.");

    if (pageNumber.HasValue && pageSize.HasValue)
    {
        blockedAttempts = blockedAttempts
            .Skip((pageNumber.Value - 1) * pageSize.Value)
            .Take(pageSize.Value)
            .ToList();
    }
    return Results.Ok(blockedAttempts);
});

app.MapPost("countries/temporal-block", (HttpContext context, string code, int duration = 1) =>
{
    
    var ip = GetClientIp(context);
   
    if (!CountriesCollection.isValidCountryCode(code))
        return Results.BadRequest("Invalid Country Code");

    if (duration < 1 || duration > 1440)
        return Results.BadRequest("Duration must be between 1 and 1440 minutes.");

    if (CountriesCollection.countries.Any(c => c.Key.Code == code))
    {
        return Results.Conflict($"Country with code {code} is already blocked.");
    }
    else
    {
        _ = CountriesCollection.addTemporaryBlockedCountry(code, ip, duration);
        return Results.Ok($"{code} Blocked");
    }
});


app.Run();



