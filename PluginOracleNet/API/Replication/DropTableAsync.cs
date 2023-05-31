using System;
using System.Threading.Tasks;
using Aunalytics.Sdk.Plugins;
using PluginOracleNet.API.Factory;
using PluginOracleNet.API.Utility;
using PluginOracleNet.DataContracts;

// --- Sourced from Firebird Plugin version 1.0.0-beta ---

namespace PluginOracleNet.API.Replication
{
    public static partial class Replication
    {
        private static readonly string DropTableQuery = @"DROP TABLE {0}.{1}";

        public static async Task DropTableAsync(IConnectionFactory connFactory, ReplicationTable table)
        {
            var conn = connFactory.GetConnection();
            var connOpen = false;

            try
            {
                if (await TableExistsAsync(connFactory, table))
                {
                    await conn.OpenAsync();
                    connOpen = true;

                    // if table exists, drop table
                    var cmd = connFactory.GetCommand(
                        string.Format(DropTableQuery,
                            Utility.Utility.GetSafeName(table.SchemaName.ToAllCaps(), '"'),
                            Utility.Utility.GetSafeName(table.TableName, '"')
                        ),
                        conn);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                if (connOpen) await conn.CloseAsync();
            }
        }
    }
}