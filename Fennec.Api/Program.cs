using Fennec.Api.FederationClient;
using Fennec.Api.Middlewares;
using Fennec.Api.Models;
using Fennec.Api.Services;
using Fennec.Api.Settings;
using Fennec.Api.Triggers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<Program>());

builder.Services.AddDbContext<FennecDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FennecDb"), npgsqlOptions =>
            npgsqlOptions.UseNodaTime()
        )
        .UseSnakeCaseNamingConvention()
        .UseTriggers(triggerOptions => triggerOptions
            .AddTrigger<SetAuditTimestampsTrigger>()
        )
        .UseProjectables()
);

builder.Services.AddSingleton<IClockService, ServerClockService>();
builder.Services.AddSingleton<IKeyService, KeyService>();

builder.Services.AddScoped<ExceptionMiddleware>();
builder.Services.AddScoped<AuthenticationMiddleware>();

builder.Services.AddOptions<KeySettings>()
    .Bind(builder.Configuration.GetSection("KeySettings"))
    .ValidateOnStart();
builder.Services.AddOptions<FennecSettings>()
    .Bind(builder.Configuration.GetSection("FennecSettings"))
    .ValidateOnStart();

builder.Services.AddSingleton<IFederationClient>(sp =>
{
    var handler = new FederationSigningHandler(
        sp.GetRequiredService<IClockService>(),
        sp.GetRequiredService<IKeyService>(),
        sp.GetRequiredService<IOptions<FennecSettings>>())
    {
        InnerHandler = new HttpClientHandler(),
    };
    var httpClient = new HttpClient(handler);
    return new FederationClient(httpClient);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FennecDbContext>();
    db.Database.Migrate();
}

app.UseMiddleware<AuthenticationMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();