using System.Collections.Generic;
using Newtonsoft.Json;

namespace PluginOracleNet.API.Replication
{
    public static partial class Replication
    {
        public static string GetSchemaJson()
        {
            Dictionary<string, object> schemaJsonObj = new Dictionary<string, object>
            {
                { "type", "object"},
                { "description", "This plugin can only write to schemas that can be accessed with the credentials used for this connection. New schemas cannot be created with the plugin. They must be created by a database administrator ahead of time." },
                { "properties", new Dictionary<string, object>
                {
                    {"SchemaName", new Dictionary<string, string>
                    {
                        {"type", "string"},
                        {"title", "Schema Name"},
                        {"description", "Name of schema to put golden and version tables into in Oracle .NET"},
                    }},
                    {"GoldenTableName", new Dictionary<string, string>
                    {
                        {"type", "string"},
                        {"title", "Golden Record Table Name"},
                        {"description", "Name for your golden record table in Oracle .NET"},
                    }},
                    {"VersionTableName", new Dictionary<string, string>
                    {
                        {"type", "string"},
                        {"title", "Version Record Table Name"},
                        {"description", "Name for your version record table in Oracle .NET"},
                    }},
                }},
                {"required", new []
                {
                    "SchemaName",
                    "GoldenTableName",
                    "VersionTableName"
                }}
            };
            
            return JsonConvert.SerializeObject(schemaJsonObj);
        }
    }
}
