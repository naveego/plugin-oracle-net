using System;
using System.Threading.Tasks;
using Aunalytics.Sdk.Plugins;
using PluginOracleNet.API.Factory;

namespace PluginOracleNet.API.Discover
{
    public static partial class Discover
    {
        public static async Task<Count> GetCountOfRecords(IConnectionFactory connFactory, Schema schema)
        {
            var query = schema.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                query = $"SELECT * FROM {schema.Id}";
            }

            var conn = connFactory.GetConnection();
            await conn.OpenAsync();

            var cmd = connFactory.GetCommand($"SELECT COUNT(*) count FROM ({query}) q", conn);
            var reader = await cmd.ExecuteReaderAsync();

            var count = -1;
            while (await reader.ReadAsync())
            {
                try
                {
                    count = Convert.ToInt32(reader.GetValueById("count"));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            await conn.CloseAsync();

            return count == -1
                ? new Count
                {
                    Kind = Count.Types.Kind.Unavailable,
                }
                : new Count
                {
                    Kind = Count.Types.Kind.Exact,
                    Value = count
                };
        }
    }
}