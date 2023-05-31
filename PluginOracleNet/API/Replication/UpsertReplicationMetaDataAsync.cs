using System;
using System.Threading.Tasks;
using Aunalytics.Sdk.Logging;
using Newtonsoft.Json;
using PluginOracleNet.API.Factory;
using PluginOracleNet.API.Utility;
using PluginOracleNet.DataContracts;
using PluginOracleNet.Helper;

// --- Sourced from Firebird Plugin version 1.0.0-beta ---

namespace PluginOracleNet.API.Replication
{
    public static partial class Replication
    {
        private static readonly string CountMetaDataQuery = $@"SELECT COUNT(*) AS ""C""
    FROM {{0}}.{{1}}
    WHERE {Utility.Utility.GetSafeName(Constants.ReplicationMetaDataJobId)} = '{{2}}'
";
        
        private static readonly string InsertMetaDataQuery = $@"INSERT INTO {{0}}.{{1}} 
(
{Utility.Utility.GetSafeName(Constants.ReplicationMetaDataJobId, '"')}
, {Utility.Utility.GetSafeName(Constants.ReplicationMetaDataRequest, '"')}
, {Utility.Utility.GetSafeName(Constants.ReplicationMetaDataReplicatedShapeId, '"')}
, {Utility.Utility.GetSafeName(Constants.ReplicationMetaDataReplicatedShapeName, '"')}
, {Utility.Utility.GetSafeName(Constants.ReplicationMetaDataTimestamp, '"')})
VALUES (
'{{2}}'
, '{{3}}'
, '{{4}}'
, '{{5}}'
, '{{6}}'
)";

        private static readonly string UpdateMetaDataQuery = $@"UPDATE {{0}}.{{1}}
SET 
{Utility.Utility.GetSafeName(Constants.ReplicationMetaDataRequest, '"')} = '{{2}}'
, {Utility.Utility.GetSafeName(Constants.ReplicationMetaDataReplicatedShapeId, '"')} = '{{3}}'
, {Utility.Utility.GetSafeName(Constants.ReplicationMetaDataReplicatedShapeName, '"')} = '{{4}}'
, {Utility.Utility.GetSafeName(Constants.ReplicationMetaDataTimestamp, '"')} = '{{5}}'
WHERE {Utility.Utility.GetSafeName(Constants.ReplicationMetaDataJobId, '"')} = '{{6}}'";

        public static async Task UpsertReplicationMetaDataAsync(IConnectionFactory connFactory, ReplicationTable table,
            ReplicationMetaData metaData)
        {
            var conn = connFactory.GetConnection();
            bool? attemptedUpdate = null;

            try
            {
                await conn.OpenAsync();
                
                // try detecting the row before attempting to insert it
                var countCmd = connFactory.GetCommand(
                    string.Format(CountMetaDataQuery,
                        Utility.Utility.GetSafeName(table.SchemaName.ToAllCaps(), '"'),
                        Utility.Utility.GetSafeName(table.TableName, '"'),
                        metaData.Request.DataVersions.JobId,
                        JsonConvert.SerializeObject(metaData.Request), //.Replace("\\", "\\\\")
                        metaData.ReplicatedShapeId,
                        metaData.ReplicatedShapeName,
                        metaData.Timestamp
                    ),
                    conn);
                var reader = await countCmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                var count = (int)Math.Round((decimal) reader.GetValueById("C"));

                if (count >= 1)
                {
                    attemptedUpdate = true;
                    
                    // update if count is above 1
                    var updateCmd = connFactory.GetCommand(
                        string.Format(UpdateMetaDataQuery,
                            Utility.Utility.GetSafeName(table.SchemaName.ToAllCaps(), '"'),
                            Utility.Utility.GetSafeName(table.TableName, '"'),
                            JsonConvert.SerializeObject(metaData.Request), //.Replace("\\", "\\\\")
                            metaData.ReplicatedShapeId,
                            metaData.ReplicatedShapeName,
                            metaData.Timestamp,
                            metaData.Request.DataVersions.JobId
                        ),
                        conn);

                    await updateCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    attemptedUpdate = false;
                    
                    // try to insert if the target row doesn't exist
                    var cmd = connFactory.GetCommand(
                        string.Format(InsertMetaDataQuery,
                            Utility.Utility.GetSafeName(table.SchemaName.ToAllCaps(), '"'),
                            Utility.Utility.GetSafeName(table.TableName, '"'),
                            metaData.Request.DataVersions.JobId,
                            JsonConvert.SerializeObject(metaData.Request), //.Replace("\\", "\\\\")
                            metaData.ReplicatedShapeId,
                            metaData.ReplicatedShapeName,
                            metaData.Timestamp
                        ),
                        conn);
                    
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception e)
            {
                switch (attemptedUpdate)
                {
                    case true:
                        Logger.Error(e, $"Error Update: {e.Message}");
                        break;
                    case false:
                        Logger.Error(e, $"Error Insert: {e.Message}");
                        break;
                    default: //case null:
                        Logger.Error(e, $"Error Counting Rows: {e.Message}");
                        break;
                }
                
                throw;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}