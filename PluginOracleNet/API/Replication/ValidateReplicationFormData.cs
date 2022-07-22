using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using PluginOracleNet.API.Factory;
using PluginOracleNet.API.Utility;
using PluginOracleNet.DataContracts;

// --- Sourced from Firebird Plugin version 1.0.0-beta ---

namespace PluginOracleNet.API.Replication
{
    public static partial class Replication
    {
        private static readonly string SchemaExistsCmd =
            "SELECT COUNT(DISTINCT username) as C FROM all_users WHERE username = '{0}' ORDER BY username";

        private static readonly Exception OracleReaderFailedException =
            new Exception("Command execution failed. No results from query.");
        
        public static async Task<List<string>> ValidateReplicationFormData(this ConfigureReplicationFormData data)
        {
            var errors = new List<string>();
            
            // 1) null or whitespace error cases
            if (string.IsNullOrWhiteSpace(data.SchemaName))
            {
                errors.Add("Schema name is empty.");
            }
            
            if (string.IsNullOrWhiteSpace(data.GoldenTableName))
            {
                errors.Add("Golden Record table name is empty.");
            }

            if (string.IsNullOrWhiteSpace(data.VersionTableName))
            {
                errors.Add("Version Record table name is empty.");
            }

            // 2) same GR and VR table
            if (data.GoldenTableName == data.VersionTableName)
            {
                errors.Add("Golden Record Table and Version Record table cannot have the same name.");
            }
            
            return errors;
        }

        private const int ValidationDelay = 1000;
        
        public static async Task<List<string>> TestReplicationFormData(ConfigureReplicationFormData data, IConnectionFactory connFactory)
        {
            var errors = new List<string>();
            var existsCheckSuccess = false;
            
            var validationTable = new ReplicationTable
            {
                SchemaName = data.SchemaName.ToAllCaps(),
                TableName = Constants.ReplicationValidationTableName,
                Columns = Constants.ReplicationValidationColumns
            };

            var conn = connFactory.GetConnection();

            // Case 1) schema does not exist
            try
            {
                await conn.OpenAsync();
                
                var schemaExistsCmd = connFactory.GetCommand(
                    string.Format(SchemaExistsCmd, data.SchemaName.ToAllCaps()), conn);

                var reader = await schemaExistsCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    // goto catch if failed to read
                    throw OracleReaderFailedException;
                }
                
                var schemaExists = (decimal)reader.GetValueById("C") > 0;

                if (!schemaExists)
                {
                    errors.Add($"Schema \"{data.SchemaName}\" does not exist in the target database.");
                }
                else
                    existsCheckSuccess = true;
            }
            catch (OracleException o)
            {
                if (o.Message.Contains("ORA-01031: ") || o.Message.Contains("ORA-00942: "))
                {
                    errors.Add("The user specified with the connection does not have proper access to the database.");
                }
                else
                {
                    var stage = existsCheckSuccess ? "checking user permissions" : "detecting the schema";
                    errors.Add($"An error occured when {stage}:\n{o.Message}.");
                }
            }
            catch (Exception e)
            {
                errors.Add(e.Message);
            }
            finally
            {
                await conn.CloseAsync();
            }
            
            if (!existsCheckSuccess) return errors;
            
            // Case 2) schema w/o privileges for all system databases
            // === Action: Attempt to create, upsert data into, and then drop the validation table ===

            // --- ensure check ---
            try
            {
                await EnsureTableAsync(connFactory, validationTable);
            }
            catch (Exception e)
            {
                errors.Add($"Unable to create test table: {e.Message}.");
            }

            if (errors.Count > 0) return errors; // exit if couldn't create table

            // --- table exists check w/ retries=5 and delay=0.5s ---
            var tableExistsSucceeded = false;
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    if (await TableExistsAsync(connFactory, validationTable))
                    {
                        tableExistsSucceeded = true;
                        break;
                    }
                }
                catch (Exception e)
                {
                    if (i >= 4) errors.Add($"Unable to verify test table: {e.Message}.");
                }

                // delay if not last retry
                if (i < 4) await Task.Delay(ValidationDelay);
            }

            if (tableExistsSucceeded)
            {
                // --- upsert check ---
                try
                {
                    var recordMap = new Dictionary<string, object>
                    {
                        [Constants.ReplicationValidationJobId] = Constants.ReplicationValidationJobIdTestValue
                    };
                    await UpsertRecordAsync(connFactory, validationTable, recordMap);
                }
                catch (Exception e)
                {
                    errors.Add($"Unable to upsert into test table: {e.Message}.");
                }

                if (errors.Count == 0)
                {
                    // --- table has record check w/ retries=5 and delay=0.5s ---
                    for (var i = 0; i < 5; i++)
                    {
                        try
                        {
                            if (await RecordExistsAsync(connFactory, validationTable,
                                    Constants.ReplicationValidationJobIdTestValue)) break;
                        }
                        catch (Exception e)
                        {
                            if (i >= 4) errors.Add($"Unable to verify test record: {e.Message}.");
                        }

                        // delay if not last retry
                        if (i < 4) await Task.Delay(ValidationDelay);
                    }
                }
            }

            // --- drop check ---
            try
            {
                await DropTableAsync(connFactory, validationTable);
            }
            catch (Exception e)
            {
                errors.Add($"Unable to drop test table: {e.Message}.");
            }

            return errors;
        }
    }
}