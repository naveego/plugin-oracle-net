using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
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

        private static readonly string SchemaPermissonsCmd = @"
SELECT COUNT(*)
  FROM DBA_TAB_PRIVS
  WHERE GRANTEE = '{0}'
    AND PRIVILEGE = 'SELECT'
    AND TABLE_NAME IN ('ALL_TABLES', 'ALL_TAB_COLUMNS',
        'ALL_CONS_COLUMNS', 'DBA_OBJECTS', 'DBA_TABLES')
".Replace("\n", " ").Replace("  ", " ");

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
        
        public static async Task<List<string>> TestReplicationFormData(ConfigureReplicationFormData data, IConnectionFactory connFactory)
        {
            var errors = new List<string>();
            var existsCheckDone = false;

            var conn = connFactory.GetConnection();

            try
            {
                await conn.OpenAsync();

                // Case 1) schema does not exist
                var schemaExistsCmd = connFactory.GetCommand(
                    string.Format(SchemaExistsCmd, data.SchemaName.ToAllCaps()), conn);

                var reader = await schemaExistsCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    // goto catch if failed to read
                    throw OracleReaderFailedException;
                }
                
                var schemaExists = (int)reader.GetValueById("C") > 0;

                if (!schemaExists)
                {
                    errors.Add($"Schema \"{data.SchemaName}\" does not exist in the target database.");
                }

                existsCheckDone = true;

                // Case 2) schema w/o select permissions for all system databases
                var schemaHasPmnsCmd = connFactory.GetCommand(
                    string.Format(SchemaPermissonsCmd, data.SchemaName.ToAllCaps()), conn);

                var reader2 = await schemaHasPmnsCmd.ExecuteReaderAsync();
                if (!await reader2.ReadAsync())
                {
                    // goto catch if failed to read
                    throw OracleReaderFailedException;
                }
                
                var schemaHasPermissions = (int)reader2.GetValueById("C") >= 5;

                if (!schemaHasPermissions)
                {
                    errors.Add("The user associated with the connection does not have proper access to the database.");
                }
            }
            catch (OracleException o)
            {
                if (o.Message.Contains("ORA-01031: "))
                {
                    errors.Add("The user associated with the connection does not have proper access to the database.");
                }
                else
                {
                    var stage = existsCheckDone ? "checking user permissions" : "finding the schema";
                    errors.Add($"An error occured when {stage}:\n{o.Message}");
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

            return errors;
        }
    }
}