using System.Collections.Concurrent;
using System.Data;
using Aegi.Vas.Dashboard.Api.Configuration;
using Aegi.Vas.Dashboard.Api.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Aegi.Vas.Dashboard.Api.Reporting;

/// <summary>
/// Reads approved KPI outputs from the Fabric Lakehouse SQL analytics endpoint.
/// Signs in as the backend's own service principal (never the executive's token).
/// Microsoft.Data.SqlClient acquires and renews the Entra token and pools connections.
/// </summary>
public sealed class FabricSqlDataSource : IReportingDataSource
{
    private static readonly ConcurrentDictionary<string, string> QueryCache = new();
    private readonly string _connectionString;
    private readonly int _timeout;
    private readonly ILogger<FabricSqlDataSource> _log;

    public string Name => "Microsoft Fabric";

    public FabricSqlDataSource(IOptions<FabricSettings> options, ILogger<FabricSqlDataSource> log)
    {
        var s = options.Value;
        _log = log;
        _timeout = s.CommandTimeoutSeconds;
        _connectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"{s.SqlEndpoint},1433",
            InitialCatalog = s.Database,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
            UserID = s.ClientId,
            Password = s.ClientSecret,
            Encrypt = SqlConnectionEncryptOption.Strict,
            ApplicationIntent = ApplicationIntent.ReadOnly,
            ApplicationName = "AEGI-VAS-Dashboard-API",
            ConnectTimeout = 30,
            Pooling = true,
        }.ConnectionString;
    }

    public Task<IReadOnlyList<VolumeRow>> GetVolumeAsync(ReportFilter f, CancellationToken ct) =>
        QueryAsync("Volume", f, null, r => new VolumeRow(r.GetString("month"), r.GetString("journey").ToLowerInvariant(), r.GetInt64Any("visas")), ct);

    public Task<IReadOnlyList<OutcomeRow>> GetOutcomesAsync(ReportFilter f, CancellationToken ct) =>
        QueryAsync("Outcomes", f, null, r => new OutcomeRow(r.GetString("outcome"), r.GetInt64Any("visas")), ct);

    public Task<IReadOnlyList<ClosureRow>> GetClosureAsync(ReportFilter f, CancellationToken ct) =>
        QueryAsync("Closure", f, null, r => new ClosureRow(r.GetString("month"), r.GetInt64Any("closed"), r.GetInt64Any("total")), ct);

    public Task<IReadOnlyList<TurnaroundRow>> GetTurnaroundAsync(ReportFilter f, string journey, CancellationToken ct) =>
        QueryAsync("Turnaround", f, journey, r => new TurnaroundRow(r.GetString("visa_type"), r.GetDecimalAny("avg_days"), r.GetInt64Any("applications")), ct);

    public Task<IReadOnlyList<RevenueRow>> GetRevenueAsync(ReportFilter f, CancellationToken ct) =>
        QueryAsync("Revenue", f, null, r => new RevenueRow(r.GetString("country"), r.GetString("centre"), r.GetDecimalAny("revenue")), ct);

    public Task<IReadOnlyList<VisaTypeAggRow>> GetVisaTypesAsync(ReportFilter f, CancellationToken ct) =>
        QueryAsync("VisaTypes", f, null, r => new VisaTypeAggRow(r.GetString("visa_type"), r.GetInt64Any("volume"),
            r.GetInt64Any("approved"), r.GetInt64Any("rejected"), r.GetInt64Any("pending"), r.GetInt64Any("closed")), ct);

    public async Task<FilterOptions> GetFilterOptionsAsync(CancellationToken ct)
    {
        var rows = await RunAsync(Sql("FilterOptions"), _ => { }, r => (
            Country: r.GetString("country"), Centre: r.GetString("centre"),
            Min: r.GetString("min_month"), Max: r.GetString("max_month")), ct);
        if (rows.Count == 0) return new FilterOptions("", "", []);
        var countries = rows.GroupBy(r => r.Country)
            .Select(g => new CountryOption(g.Key, g.Select(x => x.Centre).Distinct().OrderBy(x => x).ToList()))
            .OrderBy(c => c.Country).ToList();
        return new FilterOptions(rows.Min(r => r.Min)!, rows.Max(r => r.Max)!, countries);
    }

    public async Task<DateTimeOffset?> GetLastRefreshedUtcAsync(CancellationToken ct)
    {
        var rows = await RunAsync(Sql("LastRefreshed"), _ => { },
            r => r.IsDBNull(0) ? (DateTimeOffset?)null : new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(0), DateTimeKind.Utc)), ct);
        return rows.FirstOrDefault();
    }

    public async Task<bool> PingAsync(CancellationToken ct)
    {
        var rows = await RunAsync("SELECT 1", _ => { }, r => r.GetInt32(0), ct);
        return rows.Count == 1;
    }

    private Task<IReadOnlyList<T>> QueryAsync<T>(string name, ReportFilter f, string? journey, Func<SqlDataReader, T> map, CancellationToken ct) =>
        RunAsync(Sql(name), p =>
        {
            p.Add("@From", SqlDbType.Char, 7).Value = f.From;
            p.Add("@To", SqlDbType.Char, 7).Value = f.To;
            p.Add("@Country", SqlDbType.NVarChar, 100).Value = (object?)f.Country ?? DBNull.Value;
            p.Add("@Centre", SqlDbType.NVarChar, 100).Value = (object?)f.Centre ?? DBNull.Value;
            if (journey is not null) p.Add("@Journey", SqlDbType.NVarChar, 20).Value = journey;
        }, map, ct);

    private async Task<IReadOnlyList<T>> RunAsync<T>(string sql, Action<SqlParameterCollection> bind, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = _timeout };
            bind(cmd.Parameters);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<T>();
            while (await reader.ReadAsync(ct)) list.Add(map(reader));
            return list;
        }
        catch (SqlException ex)
        {
            _log.LogError(ex, "Fabric query failed (error {Number})", ex.Number);
            throw new ReportingUnavailableException("Reporting data is temporarily unavailable.", ex);
        }
    }

    private static string Sql(string name) => QueryCache.GetOrAdd(name, n =>
    {
        var asm = typeof(FabricSqlDataSource).Assembly;
        var resource = asm.GetManifestResourceNames().Single(r => r.EndsWith($".Reporting.Sql.{n}.sql", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(resource)!;
        return new StreamReader(stream).ReadToEnd();
    });
}

internal static class SqlReaderExtensions
{
    public static string GetString(this SqlDataReader r, string col) => Convert.ToString(r[col], System.Globalization.CultureInfo.InvariantCulture) ?? "";
    public static long GetInt64Any(this SqlDataReader r, string col) => r[col] is DBNull ? 0 : Convert.ToInt64(r[col], System.Globalization.CultureInfo.InvariantCulture);
    public static decimal GetDecimalAny(this SqlDataReader r, string col) => r[col] is DBNull ? 0 : Convert.ToDecimal(r[col], System.Globalization.CultureInfo.InvariantCulture);
}
