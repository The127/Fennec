using Fennec.Api.Models;
using Fennec.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<Program>());

builder.Services.AddDbContext<FennecDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FennecDb"), npgsqlOptions =>
        npgsqlOptions.UseNodaTime()
    ).UseSnakeCaseNamingConvention()
);

builder.Services.AddSingleton<IClockService, ServerClockService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FennecDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();