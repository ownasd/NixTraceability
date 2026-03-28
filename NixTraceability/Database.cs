using System;
using System.Data.SQLite;
using System.IO;

namespace NixTraceability
{
    public class Database
    {
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NixTraceability");

        private static readonly string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NixTraceability", "data.db");

        private static readonly string connString = $"Data Source={dbPath};Version=3;";

        static Database()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(AppDataDir);

                if (!File.Exists(dbPath))
                {
                    SQLiteConnection.CreateFile(dbPath);
                }

                using (SQLiteConnection con = new SQLiteConnection(connString))
                {
                    con.Open();

                    // ── Schema migration: PartsConfig ────────────────────────────────
                    string checkPartsCol = "PRAGMA table_info(PartsConfig)";
                    bool hasPartCode = false, hasCheckDup = false, hasImagePath = false, hasPartsRows = false;
                    using (SQLiteCommand cmd = new SQLiteCommand(checkPartsCol, con))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        hasPartsRows = reader.HasRows;
                        while (reader.Read())
                        {
                            string col = reader["name"].ToString() ?? "";
                            if (col == "PartCode") hasPartCode = true;
                            if (col == "CheckDuplicate") hasCheckDup = true;
                            if (col == "ImagePath") hasImagePath = true;
                        }
                    }

                    if (hasPartsRows && !hasPartCode)
                    {
                        // Old schema — drop and recreate
                        using (SQLiteCommand cmd = new SQLiteCommand(
                            "DROP TABLE IF EXISTS PartsConfig; DROP TABLE IF EXISTS ScanRecords; DROP TABLE IF EXISTS ScanDetails; DROP TABLE IF EXISTS Records;", con))
                            cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        if (hasPartsRows && !hasCheckDup)
                        {
                            using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE PartsConfig ADD COLUMN CheckDuplicate INTEGER DEFAULT 1;", con))
                                cmd.ExecuteNonQuery();
                        }
                        if (hasPartsRows && !hasImagePath)
                        {
                            using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE PartsConfig ADD COLUMN ImagePath TEXT DEFAULT '';", con))
                                cmd.ExecuteNonQuery();
                        }
                    }

                    // ── Schema migration: ScanRecords ────────────────────────────────
                    string checkScanCol = "PRAGMA table_info(ScanRecords)";
                    bool hasScanRows = false, hasShift = false, hasBatch = false;
                    using (SQLiteCommand cmd = new SQLiteCommand(checkScanCol, con))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        hasScanRows = reader.HasRows;
                        while (reader.Read())
                        {
                            string col = reader["name"].ToString() ?? "";
                            if (col == "Shift") hasShift = true;
                            if (col == "Batch") hasBatch = true;
                        }
                    }
                    if (hasScanRows && !hasShift)
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE ScanRecords ADD COLUMN Shift TEXT DEFAULT '';", con))
                            cmd.ExecuteNonQuery();
                    }
                    if (hasScanRows && !hasBatch)
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE ScanRecords ADD COLUMN Batch TEXT DEFAULT '';", con))
                            cmd.ExecuteNonQuery();
                    }

                    // ── Create tables ────────────────────────────────────────────────
                    string q1 = @"CREATE TABLE IF NOT EXISTS PartsConfig (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        PartName TEXT,
                        PartCode TEXT,
                        ValidationText TEXT,
                        Sequence INTEGER,
                        CheckDuplicate INTEGER DEFAULT 1,
                        IsRequired INTEGER,
                        ImagePath TEXT DEFAULT '')";

                    string q2 = @"CREATE TABLE IF NOT EXISTS ScanRecords (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DateTime DATETIME,
                        Operator TEXT,
                        Shift TEXT DEFAULT '',
                        Batch TEXT DEFAULT '',
                        Result TEXT)";

                    string q3 = @"CREATE TABLE IF NOT EXISTS ScanDetails (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        RecordId INTEGER,
                        PartCode TEXT,
                        Value TEXT,
                        Result TEXT,
                        FOREIGN KEY(RecordId) REFERENCES ScanRecords(Id))";

                    string q4 = @"CREATE TABLE IF NOT EXISTS AppSettings (
                        Key TEXT PRIMARY KEY,
                        Value TEXT)";

                    using (SQLiteCommand cmd = new SQLiteCommand(q1, con)) { cmd.ExecuteNonQuery(); }
                    using (SQLiteCommand cmd = new SQLiteCommand(q2, con)) { cmd.ExecuteNonQuery(); }
                    using (SQLiteCommand cmd = new SQLiteCommand(q3, con)) { cmd.ExecuteNonQuery(); }
                    using (SQLiteCommand cmd = new SQLiteCommand(q4, con)) { cmd.ExecuteNonQuery(); }

                    // ── Auto-backup on startup ────────────────────────────────────────
                    RunAutoBackupIfEnabled();
                }

                Logger.LogInfo("Database.InitializeDatabase", "Database initialized successfully at: " + dbPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("Database.InitializeDatabase", ex);
            }
        }

        private static void RunAutoBackupIfEnabled()
        {
            try
            {
                string autoBackup = GetSetting("AutoBackup", "1");
                if (autoBackup != "1") return;

                string backupDir = Path.Combine(AppDataDir, "backups");
                Directory.CreateDirectory(backupDir);

                string todayBackup = Path.Combine(backupDir, $"data_backup_{DateTime.Now:yyyyMMdd}.db");
                if (!File.Exists(todayBackup) && File.Exists(dbPath))
                {
                    File.Copy(dbPath, todayBackup, overwrite: false);
                    Logger.LogInfo("Database.RunAutoBackupIfEnabled", "Backup created: " + todayBackup);

                    // Clean old backups — keep last 30 days
                    var backups = Directory.GetFiles(backupDir, "data_backup_*.db");
                    Array.Sort(backups);
                    if (backups.Length > 30)
                    {
                        for (int i = 0; i < backups.Length - 30; i++)
                        {
                            File.Delete(backups[i]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Database.RunAutoBackupIfEnabled", ex);
            }
        }

        public static long InsertScanMaster(string result, string op = "OP1", string shift = "", string batch = "")
        {
            using (SQLiteConnection con = new SQLiteConnection(connString))
            {
                con.Open();
                string query = "INSERT INTO ScanRecords (DateTime, Operator, Shift, Batch, Result) VALUES (@d, @o, @sh, @bt, @r)";
                using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@d", DateTime.Now);
                    cmd.Parameters.AddWithValue("@o", op);
                    cmd.Parameters.AddWithValue("@sh", shift);
                    cmd.Parameters.AddWithValue("@bt", batch);
                    cmd.Parameters.AddWithValue("@r", result);
                    cmd.ExecuteNonQuery();
                }
                return con.LastInsertRowId;
            }
        }

        public static void InsertScanDetail(long recordId, string partCode, string value, string result)
        {
            using (SQLiteConnection con = new SQLiteConnection(connString))
            {
                con.Open();
                string query = "INSERT INTO ScanDetails (RecordId, PartCode, Value, Result) VALUES (@id, @c, @v, @r)";
                using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", recordId);
                    cmd.Parameters.AddWithValue("@c", partCode);
                    cmd.Parameters.AddWithValue("@v", value);
                    cmd.Parameters.AddWithValue("@r", result);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static bool IsDuplicateScan(string partCode, string value, string scope)
        {
            if (string.IsNullOrEmpty(value) || scope == "Sequence Only") return false;

            using (SQLiteConnection con = new SQLiteConnection(connString))
            {
                con.Open();
                string query = @"SELECT COUNT(*) FROM ScanDetails sd 
                                 INNER JOIN ScanRecords sr ON sd.RecordId = sr.Id 
                                 WHERE sd.PartCode = @c AND sd.Value = @v";

                if (scope == "Whole Day")
                    query += " AND date(sr.DateTime) = date('now', 'localtime')";
                else if (scope == "Whole Month")
                    query += " AND strftime('%Y-%m', sr.DateTime) = strftime('%Y-%m', 'now', 'localtime')";

                using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@c", partCode);
                    cmd.Parameters.AddWithValue("@v", value);
                    long count = (long)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        public static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(connString);
        }

        public static (int Total, int OK, int NG) GetTodayStats()
        {
            int ok = 0, ng = 0;
            try
            {
                using (SQLiteConnection con = new SQLiteConnection(connString))
                {
                    con.Open();
                    string query = @"SELECT Result, COUNT(*) as Count 
                                     FROM ScanRecords 
                                     WHERE date(DateTime) = date('now', 'localtime')
                                     GROUP BY Result";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string res = reader["Result"].ToString() ?? "";
                            int count = Convert.ToInt32(reader["Count"]);
                            if (res == "OK") ok = count;
                            if (res == "NG") ng = count;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Database.GetTodayStats", ex);
            }
            return (ok + ng, ok, ng);
        }

        public static string GetSetting(string key, string defaultValue = "")
        {
            try
            {
                using (SQLiteConnection con = new SQLiteConnection(connString))
                {
                    con.Open();
                    string query = "SELECT Value FROM AppSettings WHERE Key = @k";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@k", key);
                        var result = cmd.ExecuteScalar();
                        return result != null ? result.ToString() ?? defaultValue : defaultValue;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Database.GetSetting", ex);
                return defaultValue;
            }
        }

        public static void SaveSetting(string key, string? value)
        {
            try
            {
                using (SQLiteConnection con = new SQLiteConnection(connString))
                {
                    con.Open();
                    string query = "INSERT OR REPLACE INTO AppSettings (Key, Value) VALUES (@k, @v)";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@k", key);
                        cmd.Parameters.AddWithValue("@v", value ?? "");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Database.SaveSetting", ex);
            }
        }

        public static string GetDbPath() => dbPath;
        public static string GetAppDataDir() => AppDataDir;
    }
}
