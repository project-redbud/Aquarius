// 数据库升级脚本 — 在服务器上执行: dotnet run --project MigrateDb.csproj
// 或直接用: dotnet-script migrate.csx (需安装 dotnet script)

using Microsoft.Data.Sqlite;

var dbPath = args.Length > 0 ? args[0] : "Aquarius.db";
if (!File.Exists(dbPath)) { Console.WriteLine($"文件不存在: {dbPath}"); return; }

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

Console.WriteLine($"升级 {dbPath} ...");

var cmds = new[] {
    // SiteSettings 表
    @"CREATE TABLE IF NOT EXISTS SiteSettings (
        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
        SiteName TEXT NOT NULL DEFAULT 'Aquarius',
        Copyright TEXT NOT NULL DEFAULT ''
    )",
    @"INSERT INTO SiteSettings (Id, SiteName, Copyright) SELECT 1, 'Aquarius', '' WHERE NOT EXISTS (SELECT 1 FROM SiteSettings)",

    // Comments 表
    "ALTER TABLE Comments ADD COLUMN IsBottleOwnerBadge INTEGER NOT NULL DEFAULT 0",

    // Users 表
    "ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'user'",
    "ALTER TABLE Users ADD COLUMN IsBanned INTEGER NOT NULL DEFAULT 0",
    "ALTER TABLE Users ADD COLUMN BanReason TEXT NULL",
    "ALTER TABLE Users ADD COLUMN BannedUntil TEXT NULL",
    "UPDATE Users SET Role = 'admin' WHERE IsAdmin = 1",

    // Bottles 表
    "ALTER TABLE Bottles ADD COLUMN ReportedBottleId INTEGER NULL",
};

foreach (var sql in cmds)
{
    try { using var cmd = conn.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); Console.WriteLine($"  OK: {sql[..Math.Min(50, sql.Length)]}..."); }
    catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
    { Console.WriteLine($"  SKIP (already exists)"); }
    catch (Exception ex) { Console.WriteLine($"  ERR: {ex.Message}"); }
}

Console.WriteLine("升级完成!");
