using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.Tokens;
using skipper_paste;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
#if DEBUG
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Logging.SetMinimumLevel(LogLevel.Trace);
#endif
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddCors();
builder.Services.AddAuthentication();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PasteScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", "paste");
    });

var config = builder.Configuration.GetSection("Paste");
var domain = config.GetValue<string>("Domain", "localhost:5001");
var port = config.GetValue<int?>("Port", 5001);
var secret = config.GetValue<string>("Secret", "this_is_a_secret_key_for_jwt_validation");
var pasteDirectory = config.GetValue<string>("Directory", Path.Combine(AppContext.BaseDirectory, "paste-data"));


if (args.Length == 1 && args[0] == "new-token")
{
    Console.WriteLine("Generating new token...");

    //generate new JWT token based on configuration
    var claims = new[]
    {
        new System.Security.Claims.Claim("scope", "paste"),
        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "paste-user")
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
        issuer: $"https://{domain}",
        audience: "paste",
        claims: claims,
        expires: DateTime.UtcNow.AddYears(2),
        signingCredentials: creds
    );

    var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);

    Console.WriteLine("JWT Token:");
    Console.WriteLine(tokenString);
    return;
}

//add jwt token validation with secret configured from startup
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{domain}";
        options.Audience = "paste";
        options.RequireHttpsMetadata = false; // For development purposes only,
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            ValidIssuer = $"https://{domain}",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        };
    });

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.Logger.LogInformation("Checking paste directory at {directory}", pasteDirectory);

if (!Directory.Exists(pasteDirectory))
{
    app.Logger.LogInformation("Paste directory at {directory} doesn't exist!", pasteDirectory);

    Directory.CreateDirectory(pasteDirectory);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthentication();

app.MapPost("/paste", (PasteData data, HttpRequest request) =>
{
    var pasteId = RandomNameGenerator.GenerateRandomName(5);

    File.WriteAllText(Path.Combine(pasteDirectory, pasteId + ".json"), JsonSerializer.Serialize(data));

    return Results.Ok(new PasteLink(pasteId, $"{(request.IsHttps ? "https" : "http")}://{domain}/get/{pasteId}"));
})
.WithName("PasteJson")
.RequireAuthorization();

app.MapGet("/get/{id}", (string id) =>
{
    id = CheckPasteId(id);
    // Logic to retrieve the paste by id
    var pasteFile = Path.Combine(pasteDirectory, id + ".json");

    if (File.Exists(pasteFile))
    {
        return Results.Ok(JsonSerializer.Deserialize<PasteData>(File.ReadAllText(pasteFile)));
    }
    else
    {
        return Results.NotFound("Paste not found");
    }
});
    //.RequireRateLimiting();

app.Logger.LogInformation("Starting main http work...");


app.Run();

string CheckPasteId(string id)
{
    return new string([.. id.Where(c => char.IsNumber(c) || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))]);
}

public record PasteData(string Content, string Note);

public record PasteLink(string Name, string Link);

