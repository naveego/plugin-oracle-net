using System;
using System.Threading.Tasks;
using PluginOracleNet.API.Factory;
using PluginOracleNet.DataContracts;
using PluginOracleNet.API.Utility;

// --- Sourced from Firebird Plugin version 1.0.0-beta ---

namespace PluginOracleNet.API.Replication
{
    public static partial class Replication
    {
        private static readonly string RecordExistsQuery = @"SELECT COUNT(*) as ""c""
FROM (
SELECT * FROM {0}.{1}
WHERE {2} = '{3}'    
)";

        public static async Task<bool> RecordExistsAsync(IConnectionFactory connFactory, ReplicationTable table,
            string primaryKeyValue)
        {
            var conn = connFactory.GetConnection();

            try
            {
                await conn.OpenAsync();
            
                var cmd = connFactory.GetCommand(string.Format(RecordExistsQuery,
                        Utility.Utility.GetSafeName(table.SchemaName.ToAllCaps(), '"'),
                        Utility.Utility.GetSafeName(table.TableName, '"'),
                        Utility.Utility.GetSafeName(table.Columns.Find(c => c.PrimaryKey)?.ColumnName, '"'),
                        primaryKeyValue
                    ),
                    conn);

                // check if record exists
                var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                var count = (int)Math.Round((decimal) reader.GetValueById("c"));
                
                return count != 0;
            }
            finally
            {
                await conn.CloseAsync();
            }
            
        }
    }
}