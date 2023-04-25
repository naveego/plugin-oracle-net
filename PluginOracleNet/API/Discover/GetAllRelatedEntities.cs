using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aunalytics.Sdk.Plugins;
using Google.Protobuf.Collections;
using PluginOracleNet.API.Factory;

namespace PluginOracleNet.API.Discover
{
    public static partial class Discover
    {
        private const string ColSourceTableSchema = "SOURCE_TABLE_SCHEMA";
        private const string ColSourceTableName = "SOURCE_TABLE_NAME";
        private const string ColSourceColumn = "SOURCE_COLUMN";
        private const string ColForeignTableSchema = "FOREIGN_TABLE_SCHEMA";
        private const string ColForeignTableName = "FOREIGN_TABLE_NAME";
        private const string ColForeignColumn = "FOREIGN_COLUMN";
        private const string ColRelationshipName = "SOURCE_CONSTRAINT";

        private const string GetForeignKeysQuery = @"SELECT a.OWNER          SOURCE_TABLE_SCHEMA,
      a.TABLE_NAME      SOURCE_TABLE_NAME,
      a.COLUMN_NAME     SOURCE_COLUMN,
      c_pk.OWNER        FOREIGN_TABLE_SCHEMA,
      c_pk.TABLE_NAME   FOREIGN_TABLE_NAME,
      a.COLUMN_NAME     FOREIGN_COLUMN,
      c.CONSTRAINT_NAME SOURCE_CONSTRAINT
FROM all_cons_columns a
    INNER JOIN all_constraints c ON a.OWNER = c.OWNER
        AND a.CONSTRAINT_NAME = c.CONSTRAINT_NAME
    INNER JOIN all_constraints c_pk ON c.r_owner = c_pk.owner
        AND c.R_CONSTRAINT_NAME = c_pk.CONSTRAINT_NAME
WHERE c.CONSTRAINT_TYPE = 'R'
    AND c.OWNER = '{0}'
    AND a.TABLE_NAME = '{1}'
ORDER BY c.TABLE_NAME, c_pk.TABLE_NAME, c.CONSTRAINT_NAME, a.POSITION";

        public static async IAsyncEnumerable<RelatedEntity> GetAllRelatedEntities(
            IConnectionFactory connFactory,
            RepeatedField<Schema> schemas)
        {
            var conn = connFactory.GetConnection();

            try
            {
                await conn.OpenAsync();

                foreach (var schema in schemas)
                {
                    var schemaParts = DecomposeSafeName(schema.Id).TrimEscape();
                    var schemaName = schemaParts.Schema;
                    var tableName = schemaParts.Table;
                    
                    string query = string.Format(GetForeignKeysQuery, schemaName, tableName);

                    var cmd = connFactory.GetCommand(query, conn);
                    var reader = await cmd.ExecuteReaderAsync();

                    var sourceResourceId = "";
                    var sourceColumnsList = new List<string>();
                    var foreignColumnsList = new List<string>();
                    var lastRelationshipName = "";
                    var lastForeignResourceId = "";

                    var emitEntity = false;
                    var carryColumns = false;
                    var canRead = true;
                    while (canRead || lastRelationshipName != "")
                    {
                        var relationshipName = "";
                        var foreignResourceId = "";
                        
                        // delay evaluation so that the loop cycles at least once before finishing
                        canRead = await reader.ReadAsync();
                        if (canRead)
                        {
                            // read values from current row
                            sourceResourceId = $"{Utility.Utility.GetSafeName(reader.GetValueById(ColSourceTableSchema).ToString()?.Trim(' '))}.{Utility.Utility.GetSafeName(reader.GetValueById(ColSourceTableName).ToString()?.Trim(' '))}";
                            sourceColumnsList.Add(Utility.Utility.GetSafeName(reader.GetValueById(ColSourceColumn).ToString()?.Trim(' ')));
                            foreignResourceId = $"{Utility.Utility.GetSafeName(reader.GetValueById(ColForeignTableSchema).ToString()?.Trim(' '))}.{Utility.Utility.GetSafeName(reader.GetValueById(ColForeignTableName).ToString()?.Trim(' '))}";
                            foreignColumnsList.Add(Utility.Utility.GetSafeName(reader.GetValueById(ColForeignColumn).ToString()?.Trim(' ')));
                            relationshipName = $"{Utility.Utility.GetSafeName(reader.GetValueById(ColRelationshipName).ToString()?.Trim(' '))}";

                            if (lastRelationshipName != "" && lastRelationshipName != relationshipName)
                            {
                                carryColumns = true;
                                emitEntity = true;
                            }
                        }
                        else
                        {
                            carryColumns = false;
                            emitEntity = !string.IsNullOrWhiteSpace(lastRelationshipName);
                            relationshipName = "";
                        }
                        
                        // found a new foreign key constraint, emit a new related entity
                        // (skip on first couple of runs, as the related entity isn't started yet)
                        // Loops a final time after reading to finish up the related entity in progress
                        if (emitEntity)
                        {
                            var carriedColSource = "";
                            var carriedColForeign = "";
                            if (carryColumns)
                            {
                                // last column belongs to next schema, stash it for later
                                carriedColSource = sourceColumnsList.Last();
                                sourceColumnsList.RemoveAt(sourceColumnsList.Count - 1);
                                carriedColForeign = foreignColumnsList.Last();
                                foreignColumnsList.RemoveAt(foreignColumnsList.Count - 1);
                            }
                            
                            // join source & foreign resource columns into merged column strings
                            var sourceColumnBuilder = new StringBuilder();
                            var foreignColumnBuilder = new StringBuilder();
                            
                            foreach (var col in sourceColumnsList)
                            {
                                sourceColumnBuilder.AppendFormat("{0}, ", col);
                            }
                            
                            foreach (var col in foreignColumnsList)
                            {
                                foreignColumnBuilder.AppendFormat("{0}, ", col);
                            }
                            
                            // remove extra comma at the end
                            sourceColumnBuilder.Remove(sourceColumnBuilder.Length - 2, 2);
                            foreignColumnBuilder.Remove(foreignColumnBuilder.Length - 2, 2);
                            
                            var relatedEntity = new RelatedEntity
                            {
                                SchemaId = schema.Id,
                                SourceResource = sourceResourceId,
                                SourceColumn = sourceColumnBuilder.ToString(),
                                ForeignResource = lastForeignResourceId,
                                ForeignColumn = foreignColumnBuilder.ToString(),
                                RelationshipName = Math.Max(sourceColumnsList.Count, foreignColumnsList.Count) > 1
                                    ? "MULTIPART FOREIGN KEY" : "FOREIGN KEY"
                            };

                            yield return relatedEntity;

                            sourceColumnsList.Clear();
                            foreignColumnsList.Clear();

                            if (carryColumns)
                            {
                                // add stashed columns back to list
                                sourceColumnsList.Add(carriedColSource);
                                foreignColumnsList.Add(carriedColForeign);
                            }
                        }

                        lastRelationshipName = relationshipName;
                        lastForeignResourceId = foreignResourceId;
                    }
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}