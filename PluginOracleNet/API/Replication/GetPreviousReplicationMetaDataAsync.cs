using System;
using System.Threading.Tasks;
using Aunalytics.Sdk.Logging;
using Aunalytics.Sdk.Plugins;
using Newtonsoft.Json;
using PluginOracleNet.API.Factory;
using PluginOracleNet.API.Utility;
using PluginOracleNet.DataContracts;
using PluginOracleNet.Helper;
using Constants = PluginOracleNet.API.Utility.Constants;

// --- Sourced from Firebird Plugin version 1.0.0-beta ---

namespace PluginOracleNet.API.Replication
{
    public static partial class Replication
    {
        private static readonly string QueryCountMetaData = @"SELECT COUNT(*) AS ""C"" FROM {0}.{1} WHERE {2} = '{3}'";
        private static readonly string QueryGetMetaData = @"SELECT * FROM {0}.{1} WHERE {2} = '{3}'";

        public static async Task<ReplicationMetaData> GetPreviousReplicationMetaDataAsync(
            IConnectionFactory connFactory,
            string jobId,
            ReplicationTable table)
        {
            var conn = connFactory.GetConnection();

            try
            {
                ReplicationMetaData replicationMetaData = null;

                // ensure replication metadata table
                await EnsureTableAsync(connFactory, table);

                await conn.OpenAsync();

                // use count query to determine # of rows
                var countCmd = connFactory.GetCommand(
                    string.Format(QueryCountMetaData,
                        Utility.Utility.GetSafeName(table.SchemaName.ToAllCaps(), '"'),
                        Utility.Utility.GetSafeName(table.TableName, '"'),
                        Utility.Utility.GetSafeName(Constants.ReplicationMetaDataJobId),
                        jobId),
                    conn);
                var countReader = await countCmd.ExecuteReaderAsync();
                await countReader.ReadAsync();

                var metaDataCount = (int)Math.Round((decimal)countReader.GetValueById("\"C\""));
                if (metaDataCount > 0) // metadata exists
                {
                    var cmd = connFactory.GetCommand(
                        string.Format(QueryGetMetaData,
                            Utility.Utility.GetSafeName(table.SchemaName.ToAllCaps(), '"'),
                            Utility.Utility.GetSafeName(table.TableName, '"'),
                            Utility.Utility.GetSafeName(Constants.ReplicationMetaDataJobId),
                            jobId),
                        conn);
                    var reader = await cmd.ExecuteReaderAsync();

                    // if (reader.HasRows())
                    // {
                    await reader.ReadAsync();

                    var request = JsonConvert.DeserializeObject<PrepareWriteRequest>(
                        reader.GetValueById(Constants.ReplicationMetaDataRequest).ToString());
                    var shapeName = reader.GetValueById(Constants.ReplicationMetaDataReplicatedShapeName)
                        .ToString();
                    var shapeId = reader.GetValueById(Constants.ReplicationMetaDataReplicatedShapeId)
                        .ToString();
                    var timestamp = DateTime.Parse(reader.GetValueById(Constants.ReplicationMetaDataTimestamp)
                        .ToString());

                    replicationMetaData = new ReplicationMetaData
                    {
                        Request = request,
                        ReplicatedShapeName = shapeName,
                        ReplicatedShapeId = shapeId,
                        Timestamp = timestamp
                    };
                    //}
                }

                return replicationMetaData;
            }
            catch (Exception e)
            {
                Logger.Error(e, e.Message);
                throw;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}