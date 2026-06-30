using AWS.Core.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AWS.Data;

public class DynamicArchiveManager
{
    private readonly AwsDbContext _context;

    public DynamicArchiveManager(AwsDbContext context)
    {
        _context = context;
    }

    private static string TableName(int year) => $"WeighingArchive_{year}";

    public async Task EnsureTableExistsAsync(int year)
    {
        var sql = $"""
            CREATE TABLE IF NOT EXISTS {TableName(year)} (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TicketNo TEXT NOT NULL,
                VehiclePlate TEXT,
                CustomerName TEXT NOT NULL,
                GoodsName TEXT NOT NULL,
                FirstWeighTime TEXT NOT NULL,
                FirstWeight REAL NOT NULL,
                SecondWeighTime TEXT NOT NULL,
                SecondWeight REAL NOT NULL,
                GrossWeight REAL NOT NULL,
                TareWeight REAL NOT NULL,
                NetWeight REAL NOT NULL,
                OperatorName TEXT NOT NULL,
                ArchivedAt TEXT NOT NULL,
                PricePerUnit REAL,
                TotalAmount REAL,
                Remark TEXT,
                FirstWeighImagePath TEXT,
                SecondWeighImagePath TEXT
            )
            """;
        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    public async Task<long> InsertAsync(WeighingArchiveRecord record)
    {
        int year = record.ArchivedAt.Year;
        await EnsureTableExistsAsync(year);

        var sql = $"""
            INSERT INTO {TableName(year)}
                (TicketNo, VehiclePlate, CustomerName, GoodsName,
                 FirstWeighTime, FirstWeight, SecondWeighTime, SecondWeight,
                 GrossWeight, TareWeight, NetWeight, OperatorName, ArchivedAt,
                 PricePerUnit, TotalAmount, Remark, FirstWeighImagePath, SecondWeighImagePath)
            VALUES
                (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15,@p16,@p17);
            SELECT last_insert_rowid();
            """;

        var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParam(cmd, "@p0", record.TicketNo);
            AddParam(cmd, "@p1", record.VehiclePlate);
            AddParam(cmd, "@p2", record.CustomerName);
            AddParam(cmd, "@p3", record.GoodsName);
            AddParam(cmd, "@p4", record.FirstWeighTime.ToString("O"));
            AddParam(cmd, "@p5", record.FirstWeight);
            AddParam(cmd, "@p6", record.SecondWeighTime.ToString("O"));
            AddParam(cmd, "@p7", record.SecondWeight);
            AddParam(cmd, "@p8", record.GrossWeight);
            AddParam(cmd, "@p9", record.TareWeight);
            AddParam(cmd, "@p10", record.NetWeight);
            AddParam(cmd, "@p11", record.OperatorName);
            AddParam(cmd, "@p12", record.ArchivedAt.ToString("O"));
            AddParam(cmd, "@p13", (object?)record.PricePerUnit ?? DBNull.Value);
            AddParam(cmd, "@p14", (object?)record.TotalAmount ?? DBNull.Value);
            AddParam(cmd, "@p15", (object?)record.Remark ?? DBNull.Value);
            AddParam(cmd, "@p16", (object?)record.FirstWeighImagePath ?? DBNull.Value);
            AddParam(cmd, "@p17", (object?)record.SecondWeighImagePath ?? DBNull.Value);
            var id = await cmd.ExecuteScalarAsync();
            return Convert.ToInt64(id);
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    public async Task<List<WeighingArchiveRecord>> QueryAsync(int year,
        DateTime? from = null, DateTime? to = null,
        string? vehiclePlate = null, string? customerName = null, string? goodsName = null)
    {
        await EnsureTableExistsAsync(year);

        var where = new List<string>();
        if (from.HasValue) where.Add($"ArchivedAt >= '{from.Value:O}'");
        if (to.HasValue) where.Add($"ArchivedAt <= '{to.Value:O}'");
        if (!string.IsNullOrWhiteSpace(vehiclePlate)) where.Add($"VehiclePlate LIKE '%{vehiclePlate.Trim()}%'");
        if (!string.IsNullOrWhiteSpace(customerName)) where.Add($"CustomerName LIKE '%{customerName.Trim()}%'");
        if (!string.IsNullOrWhiteSpace(goodsName)) where.Add($"GoodsName LIKE '%{goodsName.Trim()}%'");

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        var sql = $"SELECT * FROM {TableName(year)} {whereClause} ORDER BY ArchivedAt DESC";

        var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<WeighingArchiveRecord>();
            while (await reader.ReadAsync())
                results.Add(MapRecord(reader));
            return results;
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    public async Task DeleteAsync(int year, long id)
    {
        await EnsureTableExistsAsync(year);
        var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM {TableName(year)} WHERE Id = @id";
            AddParam(cmd, "@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    public async Task<List<int>> GetAvailableYearsAsync()
    {
        var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'WeighingArchive_%'";
            using var reader = await cmd.ExecuteReaderAsync();
            var years = new List<int>();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                if (int.TryParse(name.Replace("WeighingArchive_", ""), out int y))
                    years.Add(y);
            }
            return years.OrderByDescending(y => y).ToList();
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    private static WeighingArchiveRecord MapRecord(System.Data.IDataReader r) => new()
    {
        Id = r.GetInt64(r.GetOrdinal("Id")),
        TicketNo = r.GetString(r.GetOrdinal("TicketNo")),
        VehiclePlate = r.IsDBNull(r.GetOrdinal("VehiclePlate")) ? null : r.GetString(r.GetOrdinal("VehiclePlate")),
        CustomerName = r.GetString(r.GetOrdinal("CustomerName")),
        GoodsName = r.GetString(r.GetOrdinal("GoodsName")),
        FirstWeighTime = DateTime.Parse(r.GetString(r.GetOrdinal("FirstWeighTime"))),
        FirstWeight = r.GetDouble(r.GetOrdinal("FirstWeight")),
        SecondWeighTime = DateTime.Parse(r.GetString(r.GetOrdinal("SecondWeighTime"))),
        SecondWeight = r.GetDouble(r.GetOrdinal("SecondWeight")),
        GrossWeight = r.GetDouble(r.GetOrdinal("GrossWeight")),
        TareWeight = r.GetDouble(r.GetOrdinal("TareWeight")),
        NetWeight = r.GetDouble(r.GetOrdinal("NetWeight")),
        OperatorName = r.GetString(r.GetOrdinal("OperatorName")),
        ArchivedAt = DateTime.Parse(r.GetString(r.GetOrdinal("ArchivedAt"))),
        PricePerUnit = r.IsDBNull(r.GetOrdinal("PricePerUnit")) ? null : r.GetDouble(r.GetOrdinal("PricePerUnit")),
        TotalAmount = r.IsDBNull(r.GetOrdinal("TotalAmount")) ? null : r.GetDouble(r.GetOrdinal("TotalAmount")),
        Remark = r.IsDBNull(r.GetOrdinal("Remark")) ? null : r.GetString(r.GetOrdinal("Remark")),
        FirstWeighImagePath  = SafeGetString(r, "FirstWeighImagePath"),
        SecondWeighImagePath = SafeGetString(r, "SecondWeighImagePath"),
    };

    private static string? SafeGetString(System.Data.IDataReader r, string col)
    {
        try
        {
            int ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? null : r.GetString(ord);
        }
        catch { return null; }
    }

    private static void AddParam(System.Data.IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
