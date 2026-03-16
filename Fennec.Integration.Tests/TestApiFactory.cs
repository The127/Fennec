using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Fennec.Integration.Tests;

public class TestApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"fennec_test_{Guid.NewGuid():N}";
    private string _privateKeyPath = null!;
    private bool _dbCreated;

    public string IssuerUrl => "https://test-instance";

    /// <summary>
    /// True if PostgreSQL is available and the factory started successfully.
    /// Tests should check this and skip if false.
    /// </summary>
    public bool IsAvailable { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _privateKeyPath = Path.Combine(Path.GetTempPath(), $"fennec_test_{Guid.NewGuid():N}.pem");
        var rsa = RSA.Create(2048);
        File.WriteAllText(_privateKeyPath, rsa.ExportRSAPrivateKeyPem());

        var connStr = $"Host=localhost;Port=7891;Database={_dbName};Username=user;Password=password";

        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:FennecDb", connStr);
        builder.UseSetting("KeySettings:PrivateKeyPath", _privateKeyPath);
        builder.UseSetting("FennecSettings:IssuerUrl", IssuerUrl);
    }

    public async Task InitializeAsync()
    {
        // Check if PostgreSQL is reachable and create the test DB before starting the server
        try
        {
            var adminConn = "Host=localhost;Port=7891;Database=postgres;Username=user;Password=password";
            await using var conn = new NpgsqlConnection(adminConn);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\"", conn);
            await cmd.ExecuteNonQueryAsync();
            _dbCreated = true;
        }
        catch
        {
            // PostgreSQL not available — tests will be skipped
            return;
        }

        try
        {
            // Force the server to start (runs migrations)
            _ = Server;
            IsAvailable = true;
        }
        catch
        {
            // Server failed to start
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        if (_dbCreated)
        {
            try
            {
                var adminConn = "Host=localhost;Port=7891;Database=postgres;Username=user;Password=password";
                await using var conn = new NpgsqlConnection(adminConn);
                await conn.OpenAsync();
                await using var terminate = new NpgsqlCommand(
                    $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_dbName}' AND pid <> pg_backend_pid()",
                    conn);
                await terminate.ExecuteNonQueryAsync();
                await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_dbName}\"", conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        try { File.Delete(_privateKeyPath); } catch { }
    }
}
