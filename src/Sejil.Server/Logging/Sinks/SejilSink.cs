// Copyright (C) 2017 Alaa Masoud
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using Sejil.Configuration;
using System.Diagnostics;

namespace Sejil.Logging.Sinks
{
    internal class SejilSink : PeriodicBatchingSink
    {
        private static readonly int _defaultBatchSizeLimit = 50;
        private static TimeSpan _defaultBatchEmitPeriod = TimeSpan.FromSeconds(5);

        private readonly string _connectionString;
        private readonly string _uri;

        public SejilSink(ISejilSettings settings) : base(_defaultBatchSizeLimit, _defaultBatchEmitPeriod)
        {
            _connectionString = $"DataSource={settings.SqliteDbPath}";
            _uri = settings.Url;

            InitializeDatabase();
        }

        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var tran = conn.BeginTransaction())
                    {
                        using (var cmdLogEntry = CreateLogEntryInsertCommand(conn, tran))
                        using (var cmdLogEntryProperty = CreateLogEntryPropertyInsertCommand(conn, tran))
                        {
                            foreach (var logEvent in events)
                            {
                                // Do not log events that were generated from browsing Sejil URL.
                                if (logEvent.Properties.Any(p => (p.Key == "RequestPath" || p.Key == "Path") &&
                                    p.Value.ToString().Contains(_uri)))
                                {
                                    continue;
                                }

                                var logId = await InsertLogEntryAsync(cmdLogEntry, logEvent);
                                foreach (var property in logEvent.Properties)
                                {
                                    await InsertLogEntryPropertyAsync(cmdLogEntryProperty, logId, logEvent.Timestamp, property);
                                }
                            }
                        }
                        tran.Commit();
                    }
                    conn.Close();
                }
            }
            catch (Exception e)
            {
                SelfLog.WriteLine(e.Message);
            }
        }

        private async Task<long> InsertLogEntryAsync(SqliteCommand cmd, LogEvent log)
        {
            cmd.Parameters["@message"].Value = log.MessageTemplate.Render(log.Properties);
            cmd.Parameters["@messageTemplate"].Value = log.MessageTemplate.Text;
            cmd.Parameters["@level"].Value = (int)log.Level;
            cmd.Parameters["@timestamp"].Value = log.Timestamp.ToUniversalTime();
            cmd.Parameters["@exception"].Value = log.Exception?.Demystify().ToString() ?? (object)DBNull.Value;
            cmd.Parameters["@sourceApp"].Value = AppDomain.CurrentDomain.FriendlyName;

            var id = await cmd.ExecuteScalarAsync();
            return (long)id;
        }

        private async Task InsertLogEntryPropertyAsync(SqliteCommand cmd, long logId, DateTimeOffset timestamp, KeyValuePair<string, LogEventPropertyValue> property)
        {
            cmd.Parameters["@logId"].Value = logId;
            cmd.Parameters["@name"].Value = property.Key;
            cmd.Parameters["@timestamp"].Value = timestamp.ToUniversalTime();
            cmd.Parameters["@value"].Value = StripStringQuotes(property.Value?.ToString()) ?? (object)DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }

        private SqliteCommand CreateLogEntryInsertCommand(SqliteConnection conn, SqliteTransaction tran)
        {
            var sql = "INSERT INTO log (message, messageTemplate, level, timestamp, exception, sourceApp)" +
                "VALUES (@message, @messageTemplate, @level, @timestamp, @exception, @sourceApp); " +
                " select last_insert_rowid();";

            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            cmd.Transaction = tran;

            cmd.Parameters.Add(new SqliteParameter("@message", DbType.String));
            cmd.Parameters.Add(new SqliteParameter("@messageTemplate", DbType.String));
            cmd.Parameters.Add(new SqliteParameter("@level", DbType.Int32));
            cmd.Parameters.Add(new SqliteParameter("@timestamp", DbType.DateTime2));
            cmd.Parameters.Add(new SqliteParameter("@exception", DbType.String));
            cmd.Parameters.Add(new SqliteParameter("@sourceApp", DbType.String));

            return cmd;
        }

        private SqliteCommand CreateLogEntryPropertyInsertCommand(SqliteConnection conn, SqliteTransaction tran)
        {
            var sql = "INSERT INTO log_property (logId, name, value, timestamp)" +
                "VALUES (@logId, @name, @value, @timestamp);";

            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            cmd.Transaction = tran;

            cmd.Parameters.Add(new SqliteParameter("@logId", DbType.Int64));
            cmd.Parameters.Add(new SqliteParameter("@name", DbType.String));
            cmd.Parameters.Add(new SqliteParameter("@value", DbType.String));
            cmd.Parameters.Add(new SqliteParameter("@timestamp", DbType.DateTime2));

            return cmd;
        }

        private void InitializeDatabase()
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = ResourceHelper.GetEmbeddedResource("Sejil.db.sql");
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private string StripStringQuotes(string value)
            => (value?.Length > 0 && value[0] == '"' && value[value.Length - 1] == '"')
                ? value.Substring(1, value.Length - 2)
                : value;
    }
}
