using System.Drawing;
using System.Threading.Tasks;
using Aunalytics.Sdk.Plugins;
using PluginOracleNet.API.Factory;
using PluginOracleNet.DataContracts;

// --- Sourced from MySQL Plugin version 1.5.2 ---

namespace PluginOracleNet.API.Write
{
    public static partial class Write
    {
        private static string ParamName = "PARAMETER_NAME";
        private static string DataType = "DATA_TYPE";

        // Source: https://dataedo.com/kb/query/oracle/list-stored-procedure-parameters
        private static string GetStoredProcedureParamsQuery = @"
select pr.owner as SCHEMA_NAME
        , pr.object_name as PROCEDURE_NAME
        , ar.argument_name as PARAMETER_NAME
        , ar.data_type as DATA_TYPE
from sys.all_procedures pr
left join sys.all_arguments ar
    on pr.object_id = ar.object_id
where pr.owner not in ('ANONYMOUS','CTXSYS','DBSNMP','EXFSYS',
          'MDSYS', 'MGMT_VIEW','OLAPSYS','OWBSYS','ORDPLUGINS', 'ORDSYS',
          'OUTLN', 'SI_INFORMTN_SCHEMA','SYS','SYSMAN','SYSTEM', 'TSMSYS',
          'WK_TEST', 'WKSYS', 'WKPROXY','WMSYS','XDB','APEX_040000',
          'APEX_PUBLIC_USER','DIP', 'FLOWS_30000','FLOWS_FILES','MDDATA',
          'ORACLE_OCM', 'XS$NULL', 'SPATIAL_CSW_ADMIN_USR', 'LBACSYS',
          'SPATIAL_WFS_ADMIN_USR', 'PUBLIC', 'APEX_040200')
        and object_type = 'PROCEDURE'
        and pr.owner = '{0}'
        and pr.object_name = '{1}'
order by SCHEMA_NAME,
        PROCEDURE_NAME,
        ar.position";
        
        /*private static string GetStoredProcedureParamsQuery = @"
SELECT PARAMETER_NAME, DATA_TYPE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.PARAMETERS
WHERE SPECIFIC_SCHEMA = '{0}'
AND SPECIFIC_NAME = '{1}'
ORDER BY ORDINAL_POSITION ASC";*/

        public static async Task<Schema> GetSchemaForStoredProcedureAsync(IConnectionFactory connFactory,
            WriteStoredProcedure storedProcedure, string goldenRecordIdParam = "")
        {
            var schema = new Schema
            {
                Id = $"{storedProcedure.GetFullName()}",
                Name = storedProcedure.GetName(),
                Description = "",
                DataFlowDirection = Schema.Types.DataFlowDirection.Write,
                Query = $"{storedProcedure.GetFullName()}"
            };

            var conn = connFactory.GetConnection();
            await conn.OpenAsync();

            var cmd = connFactory.GetCommand(
                string.Format(GetStoredProcedureParamsQuery, storedProcedure.SchemaName, storedProcedure.ProcedureName),
                conn);
            var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                // create a new property...
                var property = new Property
                {
                    Id = reader.GetValueById(ParamName).ToString(),
                    Name = reader.GetValueById(ParamName).ToString(),
                    Description = "",
                    Type = Discover.Discover.GetType(reader.GetValueById(DataType).ToString()),
                    TypeAtSource = reader.GetValueById(DataType).ToString(),
                };
                
                // mark as key if the property is to map the golden record id
                if (!string.IsNullOrWhiteSpace(goldenRecordIdParam))
                {
                    if (property.Id == goldenRecordIdParam)
                    {
                        property.IsKey = true;
                    }
                }

                schema.Properties.Add(property);
            }

            await conn.CloseAsync();

            return schema;
        }
    }
}