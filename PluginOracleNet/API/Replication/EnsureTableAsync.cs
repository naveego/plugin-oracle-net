using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Naveego.Sdk.Logging;
using Naveego.Sdk.Plugins;
using PluginOracleNet.API.Factory;
using PluginOracleNet.API.Utility;
using PluginOracleNet.DataContracts;
using PluginOracleNet.Helper;

// --- Sourced from Firebird Plugin version 1.0.0-beta ---

namespace PluginOracleNet.API.Replication
{
    public static partial class Replication
    {
//         private static readonly string EnsureTableQuery = @"SELECT COUNT(*) as c
// FROM information_schema.tables 
// WHERE table_schema = '{0}' 
// AND table_name = '{1}'";

        // Source: https://stackoverflow.com/questions/18114458/fastest-way-to-determine-if-record-exists

        private static readonly string EnsureTableQuery = @"
SELECT COUNT(*) AS C
FROM SYS.DBA_TABLES t
WHERE t.TABLESPACE_NAME NOT IN ('SYSTEM', 'SYSAUX', 'TEMP', 'DBFS_DATA')
    AND t.TABLE_NAME = '{1}' AND t.OWNER = '{0}'
";

//         private static readonly string QueryCreateTable = @"
// CREATE TABLE ""{0}""
// (
//     ""{1}"" ID INT NOT NULL
// )";

        // private static readonly string EnsureTableQuery = @"SELECT * FROM {0}.{1}";

        public static async Task<bool> TableExistsAsync(IConnectionFactory connFactory, ReplicationTable table)
        {
            var conn = connFactory.GetConnection();

            try
            {
                await conn.OpenAsync();
                
                Logger.Info(
                    $"Checking for Table: {string.Format(EnsureTableQuery, table.SchemaName.ToAllCaps(), table.TableName)}");
                var cmd = connFactory.GetCommand(
                    string.Format(EnsureTableQuery, table.SchemaName.ToAllCaps(), table.TableName), conn);
                var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                var count = (decimal)reader.GetValueById("C");

                return count > 0;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        public static async Task EnsureTableAsync(IConnectionFactory connFactory, ReplicationTable table)
        {
            var conn = connFactory.GetConnection();

            try
            {
                await conn.OpenAsync();
                
                Logger.Info(
                    $"Checking for Table: {string.Format(EnsureTableQuery, table.SchemaName.ToAllCaps(), table.TableName)}");
                var cmd = connFactory.GetCommand(
                    string.Format(EnsureTableQuery, table.SchemaName.ToAllCaps(), table.TableName), conn);
                var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                var count = (decimal)reader.GetValueById("C");

                await conn.CloseAsync();
                
                // create schema if not exists
                if (count <= 0)
                {
                    // create table statement
                    var querySb = new StringBuilder($@"CREATE TABLE {Utility.Utility.GetSafeName(table.SchemaName.ToAllCaps())}");
                    querySb.Append($".{Utility.Utility.GetSafeName(table.TableName, '"')} (");
                    querySb.Append("\n");
                    
                    // nested primary key constraint statement
                    var primaryKeySb = new StringBuilder($@"CONSTRAINT {Utility.Utility.GetSafeName(table.TableName)}");
                    primaryKeySb.Length--;
                    primaryKeySb.Append("_PK\" PRIMARY KEY (");
                    var hasPrimaryKey = false;
                    
                    foreach (var column in table.Columns)
                    {
                        querySb.Append(
                            $"{Utility.Utility.GetSafeName(column.ColumnName)} {column.DataType}{(column.PrimaryKey ? " NOT NULL" : "")},\n"
                        );

                        // skip if not primary key
                        if (!column.PrimaryKey) continue;
                        
                        // add primary key as a constraint
                        primaryKeySb.Append($"{Utility.Utility.GetSafeName(column.ColumnName)},");
                        hasPrimaryKey = true;
                    }

                    if (hasPrimaryKey)
                    {
                        primaryKeySb.Length--;
                        primaryKeySb.Append(")");
                        querySb.Append($"{primaryKeySb})");
                    }
                    else
                    {
                        querySb.Length--;
                        querySb.Append(")");
                    }

                    var query = querySb.ToString();
                    Logger.Info($"Creating Table: {query}");

                    await conn.OpenAsync();

                    var cmd2 = connFactory.GetCommand(query, conn);

                    await cmd2.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}