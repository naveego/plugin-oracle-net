using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Naveego.Sdk.Plugins;
using PluginOracleNet.API.Factory;

namespace PluginOracleNet.API.Discover
{
    public static partial class Discover
    {
        private const string GetTableAndColumnsQuery = @"
SELECT 
    t.OWNER,
    t.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.DATA_LENGTH,
    c.DATA_PRECISION,
    c.DATA_SCALE,
    c.NULLABLE,
    CASE
        WHEN tc.CONSTRAINT_TYPE = 'P'
            THEN 'P'
        ELSE NULL
    END AS CONSTRAINT_TYPE
FROM ALL_TABLES t
    INNER JOIN ALL_TAB_COLUMNS c ON c.OWNER = t.OWNER AND c.TABLE_NAME = t.TABLE_NAME
    LEFT OUTER JOIN all_cons_columns ccu ON ccu.COLUMN_NAME = c.COLUMN_NAME AND ccu.TABLE_NAME = t.TABLE_NAME AND ccu.OWNER = t.OWNER
    LEFT OUTER JOIN SYS.ALL_CONSTRAINTS tc ON tc.CONSTRAINT_NAME = ccu.CONSTRAINT_NAME AND tc.OWNER = ccu.OWNER
    WHERE TABLESPACE_NAME NOT IN ('SYSTEM', 'SYSAUX', 'TEMP', 'DBFS_DATA')
    AND t.OWNER='{0}' AND t.TABLE_NAME='{1}'
    AND (tc.CONSTRAINT_TYPE IS NULL OR tc.CONSTRAINT_TYPE = 'P')
    ORDER BY t.TABLE_NAME, c.COLUMN_ID";

        public static async Task<Schema> GetRefreshSchemaForTable(IConnectionFactory connFactory, Schema schema,
            int sampleSize = 5)
        {
            var resultSchema = await FindSchemaForTable(connFactory, schema);
            
            // get sample and count
            return await AddSampleAndCount(connFactory, resultSchema, sampleSize);
        }

        public static async Task<Schema> FindSchemaForTable(IConnectionFactory connFactory, Schema schema)
        {
            var decomposed = DecomposeSafeName(schema.Id).TrimEscape();
            var conn = connFactory.GetConnection();

            try
            {
                await conn.OpenAsync();

                var cmd = connFactory.GetCommand(
                    string.Format(GetTableAndColumnsQuery, decomposed.Schema, decomposed.Table), conn);
                var reader = await cmd.ExecuteReaderAsync();
                var refreshProperties = new List<Property>();

                while (await reader.ReadAsync())
                {
                    // add column to refreshProperties
                    var property = new Property
                    {
                        Id = Utility.Utility.GetSafeName(reader.GetValueById(ColumnName).ToString()),
                        Name = reader.GetValueById(ColumnName).ToString(),
                        IsKey = reader.GetValueById(ConstraintType).ToString() == "P",
                        IsNullable = reader.GetValueById(Nullable).ToString() == "Y",
                        Type = GetType(
                            reader.GetValueById(DataType).ToString(),
                            reader.GetValueById(DataLength),
                            reader.GetValueById(DataPrecision),
                            reader.GetValueById(DataScale)
                        ),
                        TypeAtSource = GetTypeAtSource(
                            reader.GetValueById(DataType).ToString(),
                            reader.GetValueById(DataLength),
                            reader.GetValueById(DataPrecision),
                            reader.GetValueById(DataScale)
                        )
                    };

                    var prevProp = refreshProperties.FirstOrDefault(p => p.Id == property.Id);
                    if (prevProp == null)
                    {
                        refreshProperties.Add(property);
                    }
                    else
                    {
                        var index = refreshProperties.IndexOf(prevProp);
                        refreshProperties.RemoveAt(index);

                        property.IsKey = prevProp.IsKey || property.IsKey;
                        refreshProperties.Add(property);
                    }
                }

                // add properties
                schema.Properties.Clear();
                schema.Properties.AddRange(refreshProperties);
                
                return schema;
            }
            finally
            {
                await conn.CloseAsync();   
            }
        }

        private static DecomposeResponse DecomposeSafeName(string schemaId)
        {
            var response = new DecomposeResponse
            {
                Database = "",
                Schema = "",
                Table = ""
            };
            var parts = schemaId.Split('.');

            switch (parts.Length)
            {
                case 0:
                    return response;
                case 1:
                    response.Table = parts[0];
                    return response;
                case 2:
                    response.Schema = parts[0];
                    response.Table = parts[1];
                    return response;
                case 3:
                    response.Database = parts[0];
                    response.Schema = parts[1];
                    response.Table = parts[2];
                    return response;
                default:
                    return response;
            }
        }

        private static DecomposeResponse TrimEscape(this DecomposeResponse response, char escape = '"')
        {
            response.Database = response.Database.Trim(escape);
            response.Schema = response.Schema.Trim(escape);
            response.Table = response.Table.Trim(escape);

            return response;
        }
    }

    class DecomposeResponse
    {
        public string Database { get; set; }
        public string Schema { get; set; }
        public string Table { get; set; }
    }
}