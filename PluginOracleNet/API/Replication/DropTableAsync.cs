using System;
using System.Threading.Tasks;
using Naveego.Sdk.Plugins;
using PluginOracleNet.API.Factory;
using PluginOracleNet.API.Utility;
using PluginOracleNet.DataContracts;

// --- Sourced from Firebird Plugin version 1.0.0-beta ---

namespace PluginOracleNet.API.Replication
{
    public static partial class Replication
    {
        private static readonly string TableExistsQuery = @"
select COUNT(*) as ""C""
from sys.all_tables t
where t.owner not in ('ANONYMOUS','CTXSYS','DBSNMP','EXFSYS',
          'MDSYS', 'MGMT_VIEW','OLAPSYS','OWBSYS','ORDPLUGINS', 'ORDSYS',
          'OUTLN', 'SI_INFORMTN_SCHEMA','SYS','SYSMAN','SYSTEM', 'TSMSYS',
          'WK_TEST', 'WKSYS', 'WKPROXY','WMSYS','XDB','APEX_040000',
          'APEX_PUBLIC_USER','DIP', 'FLOWS_30000','FLOWS_FILES','MDDATA',
          'ORACLE_OCM', 'XS$NULL', 'SPATIAL_CSW_ADMIN_USR', 'LBACSYS',
          'SPATIAL_WFS_ADMIN_USR', 'PUBLIC', 'APEX_040200')
    AND t.OWNER = '{0}'
    AND t.TABLE_NAME = '{1}'
";
        
        private static readonly string DropTableQuery = @"DROP TABLE {0}.{1}";

        public static async Task DropTableAsync(IConnectionFactory connFactory, ReplicationTable table)
        {
            var conn = connFactory.GetConnection();

            try
            {
                await conn.OpenAsync();
                
                // determine if table exists
                var existsCmd = connFactory.GetCommand(
                    string.Format(TableExistsQuery,
                        table.SchemaName.ToAllCaps(),
                        table.TableName
                    ), conn);
                var reader = await existsCmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                var count = (int)Math.Round((decimal)reader.GetValueById("C"));

                if (count >= 1)
                {
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
                await conn.CloseAsync();
            }
        }
    }
}