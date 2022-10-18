using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Naveego.Sdk.Plugins;
using PluginOracleNet.API.Factory;

namespace PluginOracleNet.API.Discover
{
    public static partial class Discover
    {
        private const string Owner = "OWNER";
        private const string TableName = "TABLE_NAME";
        private const string ColumnName = "COLUMN_NAME";
        private const string DataType = "DATA_TYPE";
        private const string DataLength = "DATA_LENGTH";
        private const string DataPrecision = "DATA_PRECISION";
        private const string DataScale = "DATA_SCALE";
        private const string Nullable = "NULLABLE";
        private const string ConstraintType = "CONSTRAINT_TYPE";

        private const string GetAllTablesAndColumnsQuery = @"
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
    END CONSTRAINT_TYPE
FROM ALL_TABLES t
INNER JOIN ALL_TAB_COLUMNS c ON c.OWNER = t.OWNER AND c.TABLE_NAME = t.TABLE_NAME
LEFT OUTER JOIN all_cons_columns ccu ON ccu.COLUMN_NAME = c.COLUMN_NAME AND ccu.TABLE_NAME = t.TABLE_NAME AND ccu.OWNER = t.OWNER
LEFT OUTER JOIN SYS.ALL_CONSTRAINTS tc ON tc.CONSTRAINT_NAME = ccu.CONSTRAINT_NAME AND tc.OWNER = ccu.OWNER
WHERE TABLESPACE_NAME NOT IN ('SYSTEM', 'SYSAUX', 'TEMP', 'DBFS_DATA') ORDER BY t.TABLE_NAME, c.COLUMN_ID";


        public static async IAsyncEnumerable<Schema> GetAllSchemas(IConnectionFactory connFactory, int sampleSize = 5)
        {
            var conn = connFactory.GetConnection();
            await conn.OpenAsync();

            var cmd = connFactory.GetCommand(GetAllTablesAndColumnsQuery, conn);
            var reader = await cmd.ExecuteReaderAsync();

            Schema schema = null;
            var currentSchemaId = "";
            while (await reader.ReadAsync())
            {
                var schemaId =
                    $"{Utility.Utility.GetSafeName(reader.GetValueById(Owner).ToString())}.{Utility.Utility.GetSafeName(reader.GetValueById(TableName).ToString())}";
                if (schemaId != currentSchemaId)
                {
                    // return previous schema
                    if (schema != null)
                    {
                        // get sample and count
                        yield return await AddSampleAndCount(connFactory, schema, sampleSize);
                    }

                    // start new schema
                    currentSchemaId = schemaId;
                    var parts = DecomposeSafeName(currentSchemaId).TrimEscape();
                    schema = new Schema
                    {
                        Id = currentSchemaId,
                        Name = $"{parts.Schema}.{parts.Table}",
                        Properties = { },
                        DataFlowDirection = Schema.Types.DataFlowDirection.Read
                    };
                }

                // add column to schema
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

                var prevProp = schema?.Properties.FirstOrDefault(p => p.Id == property.Id);
                if (prevProp == null)
                {
                    schema?.Properties.Add(property);
                }
                else
                {
                    var index = schema.Properties.IndexOf(prevProp);
                    schema.Properties.RemoveAt(index);

                    property.IsKey = prevProp.IsKey || property.IsKey;
                    schema.Properties.Add(property);
                }
            }

            await conn.CloseAsync();

            if (schema != null)
            {
                // get sample and count
                yield return await AddSampleAndCount(connFactory, schema, sampleSize);
            }
        }

        private static async Task<Schema> AddSampleAndCount(IConnectionFactory connFactory, Schema schema,
            int sampleSize)
        {
            // add sample and count
            var records = Read.Read.ReadRecords(connFactory, schema).Take(sampleSize);
            schema.Sample.AddRange(await records.ToListAsync());
            schema.Count = await GetCountOfRecords(connFactory, schema);

            return schema;
        }
        
        public static PropertyType GetType(string dataType, object dataLength = null, object dataPrecision = null, object dataScale = null)
        {
            dataScale ??= DBNull.Value;
            dataPrecision ??= DBNull.Value;
            
            switch (dataType)
            {
                case "DATE":
                case var t when t != null && t.Contains("TIMESTAMP"):
                    return PropertyType.Datetime;
                case "TIME":
                    return PropertyType.Time;
                case "NUMBER":
                    if ((dataScale != DBNull.Value || dataScale == null) && (dataPrecision != DBNull.Value || dataPrecision == null))
                    {
                        if ((decimal)dataScale == 0 || (decimal)dataScale == -127)
                        {
                            if ((decimal)dataPrecision <= 16)
                            {
                                return PropertyType.Integer;
                            }
                        }
                    }

                    return PropertyType.Decimal;
                case "FLOAT":
                case "BINARY_FLOAT":
                case "DOUBLE":
                case "BINARY_DOUBLE":
                    return PropertyType.Float;
                case "DECIMAL":
                case "BIGINT":
                    return PropertyType.Decimal;
                case "BOOLEAN":
                    return PropertyType.Bool;
                case "BLOB":
                    return PropertyType.Blob;
                case "XMLTYPE":
                    return PropertyType.Xml;
                case "CLOB":
                case "NCLOB":
                    return PropertyType.Text;
                case "CHAR":
                case "VARCHAR":
                case "NCHAR":
                case "NVARCHAR":
                case "VARCHAR2":
                case "NVARCHAR2":
                    if (dataLength != null)
                    {
                        if ((decimal)dataLength > 500)
                        {
                            return PropertyType.Text;
                        }
                    }
                    return PropertyType.String;
                default:
                    return PropertyType.String;
            }
        }

        private static string GetTypeAtSource(string dataType, object dataLength, object dataPrecision, object dataScale)
        {
            switch (dataType)
            {
                case "CHAR":
                case "VARCHAR2":
                case "NCHAR":
                case "NVARCHAR2":
                    if (dataLength != DBNull.Value)
                    {
                        return $"{dataType}({dataLength})";
                    }
                    break;
                case "NUMBER":
                    if (dataPrecision != DBNull.Value && dataScale != DBNull.Value)
                    {
                        return $"{dataType}({dataPrecision},{dataScale})";
                    }
                    break;
            }

            return dataType;
        }
    }
}