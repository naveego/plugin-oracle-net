using System;
using System.Text;
using System.Threading.Tasks;
using Aunalytics.Sdk.Logging;
using PluginOracleNet.API.Factory;
using PluginOracleNet.API.Utility;
using PluginOracleNet.DataContracts;

// --- Sourced from Firebird Plugin version 1.0.0-beta ---

namespace PluginOracleNet.API.Replication
{
    public static partial class Replication
    {
        private static readonly string EnsureTableQuery = "SELECT COUNT(*) FROM \"{0}\".\"{1}\"";

        public static async Task<bool> TableExistsAsync(IConnectionFactory connFactory, ReplicationTable table)
        {
            var conn = connFactory.GetConnection();

            try
            {
                await conn.OpenAsync();
                
                Logger.Info(
                    $"Checking for Table: {string.Format(EnsureTableQuery, table.SchemaName.ToAllCaps(), table.TableName)}");
                var cmd = connFactory.GetCommand(
                    string.Format(EnsureTableQuery, table.SchemaName.ToAllCaps(), table.TableName), conn);
                await cmd.ExecuteNonQueryAsync();

                return true;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("table or view does not exist")) return false;
                else throw e;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        public static async Task EnsureTableAsync(IConnectionFactory connFactory, ReplicationTable table)
        {
            var conn = connFactory.GetConnection();

            try
            {
                await conn.OpenAsync();
                
                Logger.Info(
                    $"Checking for Table: {string.Format(EnsureTableQuery, table.SchemaName.ToAllCaps(), table.TableName)}");
                var cmd = connFactory.GetCommand(
                    string.Format(EnsureTableQuery, table.SchemaName.ToAllCaps(), table.TableName), conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("table or view does not exist"))
                {
                    throw new Exception(string.Format("Unable to ensure table {0}.{1}", table.SchemaName, table.TableName), e.InnerException);
                }

                try
                {
                    // create table statement
                    var querySb = new StringBuilder($@"CREATE TABLE {Utility.Utility.GetSafeName(table.SchemaName.ToAllCaps())}");
                    querySb.Append($".{Utility.Utility.GetSafeName(table.TableName, '"')} (");
                    querySb.Append("\n");
                    
                    // nested primary key constraint statement
                    var primaryKeySb = new StringBuilder($@"CONSTRAINT {Utility.Utility.GetSafeName(table.TableName)}");
                    primaryKeySb.Length--;
                    primaryKeySb.Append("_PK\" PRIMARY KEY (");
                    var hasPrimaryKey = false;
                    
                    foreach (var column in table.Columns)
                    {
                        querySb.Append(
                            $"{Utility.Utility.GetSafeName(column.ColumnName)} {column.DataType}{(column.PrimaryKey ? " NOT NULL" : "")},\n"
                        );

                        // skip if not primary key
                        if (!column.PrimaryKey) continue;
                        
                        // add primary key as a constraint
                        primaryKeySb.Append($"{Utility.Utility.GetSafeName(column.ColumnName)},");
                        hasPrimaryKey = true;
                    }

                    if (hasPrimaryKey)
                    {
                        primaryKeySb.Length--;
                        primaryKeySb.Append(")");
                        querySb.Append($"{primaryKeySb})");
                    }
                    else
                    {
                        querySb.Length--;
                        querySb.Append(")");
                    }

                    var query = querySb.ToString();
                    Logger.Info($"Creating Table: {query}");

                    var cmd2 = connFactory.GetCommand(query, conn);

                    await cmd2.ExecuteNonQueryAsync();
                }
                catch (Exception cEx)
                {
                    throw new Exception(string.Format("Unable to ensure table {0}.{1}", table.SchemaName, table.TableName), cEx.InnerException);
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}