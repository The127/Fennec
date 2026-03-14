using Fennec.Api.Behaviors;
using Fennec.Api.FederationClient;
using Fennec.Api.Hubs;
using HttpExceptions;
using Fennec.Api.Middlewares;
using Fennec.Api.Models;
using Fennec.Api.Services;
using Fennec.Api.Settings;
using Fennec.Api.Triggers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddScoped<IMessageEventService, MessageEventService>();
builder.Services.AddScoped<INotificationEventService, NotificationEventService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IMentionParser, MentionParser>();
builder.Services.AddSingleton<VoiceStateService>();
builder.Services.AddSingleton<PresenceService>();
builder.Services.AddScoped<IVoiceEventService, VoiceEventService>();

builder.Services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

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
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ISessionTokenGenerator, RandomSessionTokenGenerator>();

builder.Services.AddScoped<RequestLoggingMiddleware>();
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

app.UseRouting();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<AuthenticationMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpExceptions();
app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<MessageHub>("/hubs/messages");
app.MapGet("/health", () => Results.Ok());

app.Run();