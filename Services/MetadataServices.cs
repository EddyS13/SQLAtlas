// Services/MetadataService.cs

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SQLAtlas.Data;
using SQLAtlas.Models;
using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Windows; // Used for MessageBox.Show in try/catch blocks

namespace SQLAtlas.Services
{
    public class MetadataService
    {
        public Dictionary<string, List<DatabaseObject>> GetDatabaseObjects()
        {
            // T-SQL to retrieve object name, type, and schema name
            string sql = @"
                SELECT 
                    o.name, 
                    o.type, 
                    o.type_desc, 
                    s.name AS SchemaName 
                FROM 
                    sys.objects o
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE 
                    o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF')
                    AND o.is_ms_shipped = 0 
                ORDER BY 
                    o.type_desc, o.name;";

            DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            var groupedObjects = new Dictionary<string, List<DatabaseObject>>();

            if (dt != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string rawType = row["type"]?.ToString()?.Trim().ToUpperInvariant() ?? "";

                    var obj = new DatabaseObject
                    {
                        Name = row["name"]?.ToString()?.Trim() ?? string.Empty,
                        SchemaName = row["SchemaName"]?.ToString()?.Trim() ?? string.Empty,
                        // CRITICAL: We need to store the SHORT CODE here for the selection logic to work
                        Type = rawType,
                        FullName = $"[{row["SchemaName"]}].[{row["name"]}]"
                    };

                    // Assign the Friendly Name to TypeDescription for the Sidebar Grouping
                    obj.TypeDescription = rawType switch
                    {
                        "U" => "Tables",
                        "V" => "Views",
                        "P" => "Stored Procedures",
                        "FN" => "Scalar Functions",
                        "TF" or "IF" => "Table Functions",
                        _ => "Other"
                    };

                    if (!groupedObjects.ContainsKey(obj.TypeDescription))
                    {
                        groupedObjects[obj.TypeDescription] = new List<DatabaseObject>();
                    }
                    groupedObjects[obj.TypeDescription].Add(obj);
                }
            }

            return groupedObjects;
        }

        public List<ColumnDetails> GetColumnDetails(string tableName)
        {
            try
            {
                // T-SQL to look up columns based on the object's name (no schema qualification needed here)
                string sql = @"
                    SELECT 
                        c.name AS ColumnName,
                        t.name AS DataType,
                        c.max_length AS MaxLength,
                        c.is_nullable AS IsNullable
                    FROM 
                        sys.columns c
                    INNER JOIN 
                        sys.types t ON c.system_type_id = t.system_type_id
                    WHERE 
                        c.object_id = OBJECT_ID(@TableName)
                    ORDER BY c.column_id;";

                var parameters = new Dictionary<string, object>
                {
                    { "@TableName", tableName }
                };

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql, parameters);
                var columns = new List<ColumnDetails>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        // Robust conversions for potentially null/non-standard fields
                        int maxLength = 0;
                        if (row["MaxLength"] != DBNull.Value)
                        {
                            int.TryParse(row["MaxLength"].ToString(), out maxLength);
                        }

                        bool isNullable = false;
                        if (row["IsNullable"] != DBNull.Value)
                        {
                            isNullable = Convert.ToBoolean(row["IsNullable"]);
                        }

                        columns.Add(new ColumnDetails
                        {
                            ColumnName = row["ColumnName"]?.ToString() ?? "N/A",
                            DataType = row["DataType"]?.ToString() ?? "N/A",
                            MaxLength = maxLength,
                            IsNullable = isNullable
                        });
                    }
                }

                return columns;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Column Data Processing Error: {ex.Message}");
                MessageBox.Show("Failed to retrieve column details. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<ColumnDetails>();
            }
        }

        public List<ForeignKeyDetails> GetForeignKeysBetween(string table1, string table2)
        {
            var list = new List<ForeignKeyDetails>();

            string sql = $@"
        SELECT 
            obj.name AS ConstraintName,
            parent.name AS ParentTable,
            referenced.name AS ReferencedTable,
            parent_col.name AS ParentColumn,
            ref_col.name AS ReferencedColumn
        FROM sys.foreign_key_columns fkc
        INNER JOIN sys.foreign_keys obj ON fkc.constraint_object_id = obj.object_id
        INNER JOIN sys.tables parent ON fkc.parent_object_id = parent.object_id
        INNER JOIN sys.tables referenced ON fkc.referenced_object_id = referenced.object_id
        INNER JOIN sys.columns parent_col ON fkc.parent_object_id = parent_col.object_id 
            AND fkc.parent_column_id = parent_col.column_id
        INNER JOIN sys.columns ref_col ON fkc.referenced_object_id = ref_col.object_id 
            AND fkc.referenced_column_id = ref_col.column_id
        WHERE 
            (parent.object_id = OBJECT_ID('{table1}') AND referenced.object_id = OBJECT_ID('{table2}'))
            OR 
            (parent.object_id = OBJECT_ID('{table2}') AND referenced.object_id = OBJECT_ID('{table1}'))";

            try
            {
                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new ForeignKeyDetails
                    {
                        ConstraintName = row["ConstraintName"].ToString() ?? "",
                        ParentTable = row["ParentTable"].ToString() ?? "",
                        ReferencedTable = row["ReferencedTable"].ToString() ?? "",
                        ParentColumn = row["ParentColumn"].ToString() ?? "",
                        ReferencedColumn = row["ReferencedColumn"].ToString() ?? ""
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }

            return list;
        }

        public List<RelationshipMap> GetTableRelationships(string selectedTableName)
        {
            try
            {
                string sql = @"
                    DECLARE @ObjectID INT = OBJECT_ID(@TableName);

                    -- If the table is not found (e.g., @ObjectID is NULL), the query will return an empty set.
                    IF @ObjectID IS NULL RETURN; 

                    -- 1. Get tables that reference the selected table ('Referenced By')
                    SELECT 
                        'Referenced By' AS RelationshipType,
                        QUOTENAME(s.name) + '.' + QUOTENAME(o.name) AS ConnectedTable
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.objects o ON fk.parent_object_id = o.object_id
                    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                    WHERE fk.referenced_object_id = @ObjectID

                    UNION ALL

                    -- 2. Get tables that the selected table references ('References')
                    SELECT 
                        'References' AS RelationshipType,
                        QUOTENAME(s.name) + '.' + QUOTENAME(o.name) AS ConnectedTable
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.objects o ON fk.referenced_object_id = o.object_id
                    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                    WHERE fk.parent_object_id = @ObjectID;";

                var parameters = new Dictionary<string, object>
                {
                    { "@TableName", selectedTableName }
                };

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql, parameters);
                var relationshipMaps = new List<RelationshipMap>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        relationshipMaps.Add(new RelationshipMap
                        {
                            RelationshipType = row["RelationshipType"]?.ToString() ?? string.Empty,
                            ConnectedTable = row["ConnectedTable"]?.ToString() ?? string.Empty,
                        });
                    }
                }
                return relationshipMaps;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Relationship Data Processing Error: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<RelationshipMap>();
            }
        }

        // CRITICAL FIX #1: GetObjectDetails() - SQL Injection
        public ProcedureDetails GetObjectDetails(string schemaName, string objectName)
        {
            // SECURITY: Validate inputs
            if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(objectName))
            {
                return new ProcedureDetails 
                { 
                    Definition = "ERROR: Invalid schema or object name.",
                    Parameters = new List<ProcedureParameter>()
                };
            }

            string qualifiedName = $"[{schemaName}].[{objectName}]";

            // 1. Get Definition and Dates - NOW USING PARAMETERIZED QUERY
            string defSql = @"
                SELECT 
                    m.definition AS Definition,
                    o.create_date AS CreateDate,
                    o.modify_date AS ModifyDate
                FROM sys.sql_modules m
                INNER JOIN sys.objects o ON m.object_id = o.object_id
                WHERE m.object_id = OBJECT_ID(@QualifiedName)";

            var parameters = new Dictionary<string, object>
            {
                { "@QualifiedName", qualifiedName }
            };

            DataTable defDt = SqlConnectionManager.ExecuteQuery(defSql, parameters);

            // Initialize with defaults
            string definition = "DEFINITION NOT FOUND";
            DateTime createDate = DateTime.MinValue;
            DateTime modifyDate = DateTime.MinValue;

            if (defDt is not null && defDt.Rows.Count > 0)
            {
                object definitionValue = defDt.Rows[0]["Definition"];

                if (definitionValue is DBNull)
                {
                    definition = "DEFINITION IS ENCRYPTED";
                }
                else
                {
                    definition = definitionValue.ToString() ?? "DEFINITION NOT FOUND";
                }

                object createDateValue = defDt.Rows[0]["CreateDate"];
                object modifyDateValue = defDt.Rows[0]["ModifyDate"];

                createDate = (createDateValue is not DBNull)
                             ? Convert.ToDateTime(createDateValue)
                             : DateTime.MinValue;

                modifyDate = (modifyDateValue is not DBNull)
                             ? Convert.ToDateTime(modifyDateValue)
                             : DateTime.MinValue;
            }

            // 2. Get Parameters - NOW USING PARAMETERIZED QUERY
            string paramSql = @"
                SELECT p.name AS Name, t.name AS DataType
                FROM sys.parameters p
                INNER JOIN sys.types t ON p.system_type_id = t.system_type_id
                WHERE p.object_id = OBJECT_ID(@QualifiedName)
                ORDER BY p.parameter_id;";

            var paramParameters = new Dictionary<string, object>
            {
                { "@QualifiedName", qualifiedName }
            };

            DataTable paramDt = SqlConnectionManager.ExecuteQuery(paramSql, paramParameters);
            var procParameters = new List<ProcedureParameter>();

            if (paramDt != null)
            {
                foreach (DataRow row in paramDt.Rows)
                {
                    procParameters.Add(new ProcedureParameter
                    {
                        Name = row["Name"]?.ToString() ?? string.Empty,
                        DataType = row["DataType"]?.ToString() ?? string.Empty,
                    });
                }
            }

            return new ProcedureDetails
            {
                Definition = definition,
                Parameters = procParameters,
                CreateDate = createDate,
                ModifyDate = modifyDate
            };
        }

        public List<Dependency> GetObjectDependencies(string schemaName, string objectName)
        {
            try
            {
                // SECURITY: Validate inputs
                if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(objectName))
                {
                    return new List<Dependency>();
                }

                string qualifiedName = $"[{schemaName}].[{objectName}]";

                // FIXED: Use parameterized query instead of string interpolation
                string objectIdSql = "SELECT OBJECT_ID(@QualifiedName)";
                var idParameters = new Dictionary<string, object>
                {
                    { "@QualifiedName", qualifiedName }
                };

                object? objectIdResult = SqlConnectionManager.ExecuteScalar(objectIdSql, idParameters);

                if (objectIdResult is null || objectIdResult is DBNull)
                {
                    return new List<Dependency>();
                }

                int objectId = Convert.ToInt32(objectIdResult);

                string sql = @"
                    SELECT
                        CASE 
                            WHEN o_ref.type IN ('U', 'V') THEN 'Touches Table/View' 
                            WHEN o_ref.type IN ('P', 'FN', 'IF', 'TF') THEN 'Calls Stored Proc/Func'
                            ELSE 'Other'
                        END AS Type,
                        QUOTENAME(s_ref.name) + '.' + QUOTENAME(o_ref.name) AS ObjectName
                    FROM sys.sql_expression_dependencies dep
                    INNER JOIN sys.objects o_ref ON dep.referenced_id = o_ref.object_id
                    INNER JOIN sys.schemas s_ref ON o_ref.schema_id = s_ref.schema_id
                    WHERE dep.referencing_id = OBJECT_ID(@QualifiedName);";

                var parameters = new Dictionary<string, object> 
                { 
                    { "@QualifiedName", qualifiedName } 
                };
                
                DataTable dt = SqlConnectionManager.ExecuteQuery(sql, parameters);
                var dependencies = new List<Dependency>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        dependencies.Add(new Dependency
                        {
                            Type = row["Type"]?.ToString() ?? string.Empty,
                            ObjectName = row["ObjectName"]?.ToString() ?? string.Empty,
                        });
                    }
                }
                return dependencies;
            }
            catch (Exception ex)
            {
                // SECURITY FIX: Don't expose database schema in error messages
                MessageBox.Show("An error occurred while retrieving dependencies. Please try again.", 
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Dependency Lookup Error: {ex.Message}");
                return new List<Dependency>();
            }
        }

        public List<DatabaseActivity> GetDatabaseActivity()
        {
            // T-SQL to retrieve current active sessions/requests with connection time for robust display
            string sql = @"
                SELECT TOP 100
                    s.session_id AS SessionID, 
                    s.login_name AS LoginName, 
                    s.host_name AS HostName,
                    s.login_time AS LoginTime,  
                    r.status AS Status, 
                    r.command AS Command, 
                    r.start_time AS StartTime
                FROM sys.dm_exec_sessions s
                LEFT JOIN sys.dm_exec_requests r ON s.session_id = r.session_id
                WHERE s.is_user_process = 1
                  AND s.session_id <> @@SPID 
                ORDER BY ISNULL(r.start_time, s.login_time) DESC;";

            DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
            var activities = new List<DatabaseActivity>();

            if (dt != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                    DateTime loginTime = (row["LoginTime"] != DBNull.Value) ? Convert.ToDateTime(row["LoginTime"]) : DateTime.MinValue;

                    activities.Add(new DatabaseActivity
                    {
                        SessionID = Convert.ToInt16(row["SessionID"]),
                        // Safe string casting using null-coalescing
                        LoginName = row["LoginName"]?.ToString() ?? "N/A",
                        HostName = row["HostName"]?.ToString() ?? "N/A",
                        LoginTime = loginTime,

                        // Handle DBNull for nullable fields
                        Status = (row["Status"] is not DBNull)
                             ? row["Status"]?.ToString() ?? "N/A"
                             : "SLEEPING",

                        Command = (row["Command"] is not DBNull)
                              ? row["Command"]?.ToString() ?? "N/A"
                              : "NONE",

                        // If StartTime is null (sleeping session), use LoginTime
                        StartTime = (row["StartTime"] != DBNull.Value) ? Convert.ToDateTime(row["StartTime"]) : loginTime
                    });
                }
            }
            return activities;
        }

        public List<LongRunningQuery> GetLongRunningQueries()
        {
            try
            {
                // T-SQL to find the TOP 5 longest running active requests
                string sql = @"
                    SELECT TOP 5
                        r.session_id AS SessionId,
                        r.status AS Status,
                        DATEDIFF(SECOND, r.start_time, GETDATE()) AS DurationSeconds,
                        t.text AS SqlStatement
                    FROM sys.dm_exec_requests r
                    CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
                    WHERE r.session_id<> @@SPID
                    ORDER BY r.start_time ASC; ";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var queries = new List<LongRunningQuery>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        // 1. Safely retrieve the raw SQL statement object (can be DBNull)
                        object sqlObj = row["SqlStatement"];

                        // 2. Convert to string, or default to "N/A" if NULL/DBNull is found.
                        string fullSql = (sqlObj is DBNull) ? "N/A" : sqlObj.ToString() ?? "N/A";

                        // 3. Perform safe truncation: Math.Min ensures we don't try to exceed the string length.
                        string truncatedSql = fullSql.Substring(0, Math.Min(fullSql.Length, 500));

                        queries.Add(new LongRunningQuery
                        {
                            SessionId = Convert.ToInt16(row["SessionId"]),
                            Status = row["Status"]?.ToString() ?? "N/A",
                            CommandDurationSeconds = Convert.ToInt32(row["DurationSeconds"]),

                            // Assign the safely truncated string
                            SqlStatement = truncatedSql
                        });
                    }
                }
                return queries;
            }
            catch (Exception ex)
            {
                // Log the error and return an empty list on failure
                MessageBox.Show($"Performance Monitor Error: {ex.Message}", "DMV Access Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<LongRunningQuery>();
            }
        }

        public List<MissingIndex> GetMissingIndexes()
        {
            var list = new List<MissingIndex>();
            string sql = @"
                SELECT 
                    d.statement AS TableName,
                    gs.avg_user_impact AS Impact,
                    d.equality_columns AS EqualityColumns,
                    d.inequality_columns AS InequalityColumns,
                    d.included_columns AS IncludedColumns,
                    'CREATE INDEX [IX_MISSING_' + CAST(gs.group_handle AS VARCHAR) + '] ON ' + d.statement + 
                    ' (' + ISNULL(d.equality_columns, '') + 
                    CASE WHEN d.equality_columns IS NOT NULL AND d.inequality_columns IS NOT NULL THEN ',' ELSE '' END + 
                    ISNULL(d.inequality_columns, '') + ')' + 
                    ISNULL(' INCLUDE (' + d.included_columns + ')', '') AS CreateScript
                FROM sys.dm_db_missing_index_groups g
                JOIN sys.dm_db_missing_index_group_stats gs ON gs.group_handle = g.index_group_handle
                JOIN sys.dm_db_missing_index_details d ON g.index_handle = d.index_handle
                WHERE d.database_id = DB_ID()
                ORDER BY gs.avg_user_impact DESC";

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new MissingIndex
                {
                    TableName = row["TableName"].ToString() ?? "",
                    Impact = Convert.ToDouble(row["Impact"]),
                    EqualityColumns = row["EqualityColumns"]?.ToString() ?? "N/A",
                    InequalityColumns = row["InequalityColumns"]?.ToString() ?? "N/A",
                    IncludedColumns = row["IncludedColumns"]?.ToString() ?? "N/A",
                    CreateScript = row["CreateScript"].ToString() ?? ""
                });
            }
            return list;
        }

        public List<DatabaseSpaceInfo> GetDatabaseSpaceInfo()
        {
            try
            {
                // T-SQL to retrieve file size and usage information for the current database.
                // File sizes are returned in 8KB pages, so we divide by 128 to get MB.
                string sql = @"
            SELECT 
                fg.name AS FileGroupName,
                f.name AS FileName,
                f.type_desc AS FileType,
                CAST((f.size * 8.0 / 1024) AS DECIMAL(10, 2)) AS CurrentSizeMB,
                CAST((f.size - FILEPROPERTY(f.name, 'SpaceUsed')) * 8.0 / 1024 AS DECIMAL(10, 2)) AS AvailableFreeSpaceMB,
                CASE f.max_size 
                    WHEN 0 THEN 'No Growth Allowed'
                    WHEN -1 THEN 'Unlimited'
                    ELSE CAST((f.max_size * 8.0 / 1024) AS NVARCHAR(20)) + ' MB'
                END AS MaxSize
            FROM sys.database_files f
            LEFT JOIN sys.filegroups fg ON f.data_space_id = fg.data_space_id
            ORDER BY f.type_desc DESC, f.name ASC;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var spaceInfoList = new List<DatabaseSpaceInfo>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        spaceInfoList.Add(new DatabaseSpaceInfo
                        {
                            FileGroupName = row["FileGroupName"]?.ToString() ?? "N/A",
                            FileName = row["FileName"]?.ToString() ?? "N/A",
                            FileType = row["FileType"]?.ToString() ?? "N/A",
                            CurrentSizeMB = row["CurrentSizeMB"]?.ToString() ?? "0.00",
                            AvailableFreeSpaceMB = row["AvailableFreeSpaceMB"]?.ToString() ?? "0.00",
                            MaxSize = row["MaxSize"]?.ToString() ?? "N/A"
                        });
                    }
                }
                return spaceInfoList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Space Access Error: {ex.Message}", "Admin Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<DatabaseSpaceInfo>();
            }
        }

        public List<ObjectPermission> GetObjectPermissions(string objectName)
        {
            try
            {
                // T-SQL to retrieve permissions for a specific object, joining principals to get names.
                string sql = @"
            SELECT 
                dp.permission_name AS PermissionType,
                dp.state_desc AS PermissionState,
                p.name AS PrincipalName
            FROM sys.database_permissions dp
            INNER JOIN sys.database_principals p ON dp.grantee_principal_id = p.principal_id
            WHERE 
                dp.major_id = OBJECT_ID(@ObjectName)
                AND dp.class = 1 -- Class 1 means object or column permissions
            ORDER BY 
                p.name, dp.permission_name;";

                var parameters = new Dictionary<string, object>
        {
            { "@ObjectName", objectName }
        };

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql, parameters);
                var permissions = new List<ObjectPermission>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        permissions.Add(new ObjectPermission
                        {
                            PrincipalName = row["PrincipalName"]?.ToString() ?? "N/A",
                            PermissionType = row["PermissionType"]?.ToString() ?? "N/A",
                            PermissionState = row["PermissionState"]?.ToString() ?? "N/A"
                        });
                    }
                }
                return permissions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Object Permissions Access Error: {ex.Message}", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<ObjectPermission>();
            }
        }

        public List<ExpensiveQuery> GetTopExpensiveQueries()
        {
            var list = new List<ExpensiveQuery>();
            string sql = @"
        SELECT TOP 10
            qs.execution_count AS ExecutionCount,
            (qs.total_worker_time / 1000) / qs.execution_count AS AvgCpuTime,
            qs.total_logical_reads / qs.execution_count AS AvgLogicalReads,
            (qs.total_logical_reads + qs.total_logical_writes) / qs.execution_count AS AvgIo,
            qs.last_execution_time AS LastExecutionTime,
            SUBSTRING(st.text, (qs.statement_start_offset/2) + 1,
            ((CASE qs.statement_end_offset
                WHEN -1 THEN DATALENGTH(st.text)
                ELSE qs.statement_end_offset END 
                    - qs.statement_start_offset)/2) + 1) AS QueryText
        FROM sys.dm_exec_query_stats AS qs
        CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
        ORDER BY qs.total_logical_reads DESC";

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new ExpensiveQuery
                {
                    ExecutionCount = Convert.ToInt64(row["ExecutionCount"]),
                    AvgCpuTime = Convert.ToInt64(row["AvgCpuTime"]),
                    AvgLogicalReads = Convert.ToInt64(row["AvgLogicalReads"]),
                    AvgIo = Convert.ToInt64(row["AvgIo"]),
                    LastExecutionTime = Convert.ToDateTime(row["LastExecutionTime"]),
                    QueryText = row["QueryText"].ToString() ?? ""
                });
            }
            return list;
        }

        //public List<CachedQueryMetric> GetTopExpensiveQueries()
        //{
        //    try
        //    {
        //        // T-SQL to find the TOP 10 most expensive queries by Total Logical Reads (I/O)
        //        string sql = @"
        //    SELECT TOP 10
        //        qs.execution_count AS ExecutionCount,
        //        qs.total_worker_time AS TotalCPUTime,
        //        qs.total_logical_reads AS TotalLogicalReads,
        //        qs.total_worker_time / qs.execution_count AS AvgCPUTime,
        //        SUBSTRING(st.text, (qs.statement_start_offset/2)+1, 
        //            ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(st.text) 
        //              ELSE qs.statement_end_offset END - qs.statement_start_offset)/2)+1) AS QueryStatement
        //    FROM sys.dm_exec_query_stats qs
        //    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
        //    ORDER BY TotalLogicalReads DESC;";

        //        DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
        //        var metrics = new List<CachedQueryMetric>();

        //        if (dt is not null)
        //        {
        //            foreach (DataRow row in dt.Rows)
        //            {
        //                // Safely retrieve the query statement object (can be DBNull)
        //                object queryStatementObj = row["QueryStatement"];

        //                // Convert to string, or default to string.Empty if DBNull/Null is found.
        //                string fullQueryStatement = (queryStatementObj is DBNull)
        //                                            ? string.Empty
        //                                            : queryStatementObj.ToString() ?? string.Empty;

        //                // Trim the whitespace from the resulting string.
        //                string trimmedStatement = fullQueryStatement.Trim();

        //                metrics.Add(new CachedQueryMetric
        //                {
        //                    ExecutionCount = Convert.ToInt64(row["ExecutionCount"]),
        //                    TotalCPUTimeMS = (Convert.ToInt64(row["TotalCPUTime"]) / 1000).ToString("N0"),
        //                    TotalLogicalReads = Convert.ToInt64(row["TotalLogicalReads"]).ToString("N0"),
        //                    AvgCPUTimeMS = (Convert.ToInt64(row["AvgCPUTime"]) / 1000).ToString("N0"),

        //                    // Assign the safely retrieved and trimmed string
        //                    QueryStatement = trimmedStatement
        //                });
        //            }
        //        }
        //        return metrics;
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Expensive Queries Access Error: {ex.Message}", "Optimization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //        return new List<CachedQueryMetric>();
        //    }
        //}

        public List<WaitStat> GetTopWaits()
        {
            var list = new List<WaitStat>();
            string sql = @"
                WITH WaitStats AS (
                    SELECT wait_type, wait_time_ms / 1000.0 AS WaitS,
                    (wait_time_ms - signal_wait_time_ms) / 1000.0 AS ResourceS,
                    signal_wait_time_ms / 1000.0 AS SignalS,
                    waiting_tasks_count AS WaitCount,
                    100.0 * wait_time_ms / SUM(wait_time_ms) OVER() AS Percentage,
                    ROW_NUMBER() OVER(ORDER BY wait_time_ms DESC) AS RowNum
                    FROM sys.dm_os_wait_stats
                    WHERE [wait_type] NOT IN (
                        'CLR_SEMAPHORE', 'LAZYWRITER_SLEEP', 'OLD_JOB_CHECK', 'SQLTRACE_BUFFER_FLUSH',
                        'SLEEP_TASK', 'SLEEP_SYSTEMTASK', 'CHECKPOINT_QUEUE', 'REQUEST_FOR_DEADLOCK_SEARCH',
                        'XE_TIMER_EVENT', 'XE_DISPATCHER_WAIT', 'FT_IFTS_SCHEDULER_IDLE_WAIT',
                        'BROKER_EVENTHANDLER', 'TRACEWRITE', 'FT_IFTSHC_MUTEX', 'LOGMGR_QUEUE',
                        'CHECKPOINT_WAIT', 'PREEMPTIVE_OS_AUTHENTICATIONOPS', 'BROKER_TRANSMITTER'
                    )
                )
                SELECT TOP 10 wait_type, WaitS, Percentage, 
                       CASE WHEN WaitCount = 0 THEN 0 ELSE (WaitS * 1000) / WaitCount END AS AvgWaitMs
                FROM WaitStats
                WHERE Percentage > 0.1
                ORDER BY WaitS DESC";

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                string type = row["wait_type"].ToString();
                list.Add(new WaitStat
                {
                    WaitType = type,
                    WaitTimeS = Convert.ToDouble(row["WaitS"]),
                    Percentage = Convert.ToDouble(row["Percentage"]) / 100.0,
                    AvgWaitMs = Convert.ToDouble(row["AvgWaitMs"]),
                    Description = GetWaitDescription(type)
                });
            }
            return list;
        }

        // Simple helper to provide context for common SQL waits
        private string GetWaitDescription(string waitType)
        {
            return waitType switch
            {
                "CXPACKET" => "Parallelism issues. Check for missing indexes or high cost of threshold for parallelism.",
                "SOS_SCHEDULER_YIELD" => "CPU Pressure. The CPU is struggling to keep up with the workload.",
                "PAGEIOLATCH_SH" => "Disk Reads. Data is being pulled from disk into the buffer pool (Slow Disk or Missing Indexes).",
                "PAGEIOLATCH_EX" => "Disk Writes. Slow disk performance during data modifications.",
                "LCK_M_X" => "Exclusive Locks. Processes are waiting for other transactions to release data locks.",
                "WRITELOG" => "Transaction Log I/O. The disk hosting the .ldf file is too slow.",
                "ASYNC_NETWORK_IO" => "Network or Client issue. The client isn't consuming data fast enough.",
                _ => "General system wait event."
            };
        }

        public List<IndexFragmentation> GetIndexFragmentation()
        {
            var list = new List<IndexFragmentation>();

            // Using sys.dm_db_index_physical_stats in 'LIMITED' mode for high performance
            // We join with sys.indexes to get the human-readable index name
            string sql = @"
                SELECT 
                    OBJECT_SCHEMA_NAME(ips.object_id) + '.' + OBJECT_NAME(ips.object_id) AS SchemaTableName,
                    ISNULL(i.name, 'HEAP') AS IndexName,
                    ips.avg_fragmentation_in_percent AS FragmentationPercent,
                    ips.page_count AS PageCount
                FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
                INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
                WHERE ips.index_id > 0  -- Exclude Heaps for maintenance recommendations
                  AND ips.page_count > 8 -- Filter out tiny indexes that always show high frag
                ORDER BY ips.avg_fragmentation_in_percent DESC";

            try
            {
                System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    list.Add(new IndexFragmentation
                    {
                        SchemaTableName = row["SchemaTableName"].ToString() ?? "Unknown",
                        IndexName = row["IndexName"].ToString() ?? "N/A",
                        FragmentationPercent = Convert.ToDouble(row["FragmentationPercent"]),
                        PageCount = Convert.ToInt64(row["PageCount"])
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error or rethrow to be caught by the View's try-catch
                throw new Exception("Error calculating index fragmentation: " + ex.Message);
            }

            return list;
        }

        public List<BlockingProcess> GetCurrentBlockingChain()
        {
            var list = new List<BlockingProcess>();
            string sql = @"
                WITH BlockingTree AS (
                    SELECT 
                        session_id AS Spid, 
                        blocking_session_id AS BlockingSpid,
                        wait_time AS WaitTimeMs,
                        0 AS Level
                    FROM sys.dm_exec_requests
                    WHERE blocking_session_id = 0 AND session_id IN (SELECT blocking_session_id FROM sys.dm_exec_requests WHERE blocking_session_id <> 0)
            
                    UNION ALL
            
                    SELECT 
                        r.session_id, 
                        r.blocking_session_id,
                        r.wait_time,
                        bt.Level + 1
                    FROM sys.dm_exec_requests r
                    INNER JOIN BlockingTree bt ON r.blocking_session_id = bt.Spid
                )
                SELECT bt.*, st.text AS QueryText
                FROM BlockingTree bt
                CROSS APPLY sys.dm_exec_sql_text((SELECT sql_handle FROM sys.dm_exec_requests WHERE session_id = bt.Spid)) st
                ORDER BY Level, WaitTimeMs DESC";

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new BlockingProcess
                {
                    Spid = Convert.ToInt32(row["Spid"]),
                    BlockingSpid = Convert.ToInt32(row["BlockingSpid"]),
                    WaitTimeMs = Convert.ToInt64(row["WaitTimeMs"]),
                    BlockLevel = Convert.ToInt32(row["Level"]),
                    QueryText = row["QueryText"].ToString()?.Trim() ?? "Unknown"
                });
            }
            return list;
        }

        public string GetPlanCacheSize()
        {
            try
            {
                // T-SQL to get the total memory consumed by the query plan cache
                string sql = @"
            SELECT 
                CAST(SUM(pages_kb) / 1024.0 AS DECIMAL(10, 2))
            FROM sys.dm_os_memory_clerks 
            WHERE type = 'CACHESTORE_SQLCP' OR type = 'CACHESTORE_OBJCP';";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    return dt.Rows[0][0]?.ToString() + " MB";
                }
                return "N/A";
            }
            catch
            {
                return "Error";
            }
        }

        public List<DatabasePrincipal> GetDatabasePrincipals()
        {
            var list = new List<DatabasePrincipal>();
            string sql = @"
                SELECT 
                    name, 
                    type_desc AS TypeDescription, 
                    create_date AS CreateDate, 
                    default_schema_name AS DefaultSchema,
                    authentication_type_desc AS AuthenticationType
                FROM sys.database_principals
                WHERE type IN ('S', 'U', 'G') -- SQL User, Windows User, Windows Group
                ORDER BY name ASC";

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new DatabasePrincipal
                {
                    Name = row["name"].ToString() ?? "",
                    TypeDescription = row["TypeDescription"].ToString() ?? "",
                    CreateDate = Convert.ToDateTime(row["CreateDate"]),
                    DefaultSchema = row["DefaultSchema"].ToString() ?? "N/A",
                    AuthenticationType = row["AuthenticationType"].ToString() ?? "NONE"
                });
            }
            return list;
        }

        public List<RoleMembership> GetRoleMemberships()
        {
            var list = new List<RoleMembership>();
            string sql = @"
                SELECT 
                    DP1.name AS MemberName, 
                    DP1.type_desc AS MemberType, 
                    DP2.name AS RoleName
                FROM sys.database_role_members AS DRM
                INNER JOIN sys.database_principals AS DP1 ON DRM.member_principal_id = DP1.principal_id
                INNER JOIN sys.database_principals AS DP2 ON DRM.role_principal_id = DP2.principal_id
                WHERE DP1.name NOT IN ('dbo', 'sys', 'information_schema')
                ORDER BY RoleName, MemberName";

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new RoleMembership
                {
                    MemberName = row["MemberName"].ToString() ?? "",
                    MemberType = row["MemberType"].ToString() ?? "",
                    RoleName = row["RoleName"].ToString() ?? ""
                });
            }
            return list;
        }


        public List<AuditLogEvent> GetRecentSecurityEvents()
        {
            var list = new List<AuditLogEvent>();

            // We pull the last 500-1000 entries from the current error log (0)
            // 1 = SQL Server Log
            string sql = "EXEC xp_readerrorlog 0, 1";

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            // Define security keywords we care about
            string[] securityKeywords = {
                "Login failed",
                "Error:",
                "Severity:",
                "failed",
                "denied",
                "Encryption",
                "Audit",
                "Database Mirroring",
                "Connection reset"
            };

            foreach (System.Data.DataRow row in dt.Rows)
            {
                string message = row["Text"].ToString() ?? "";

                // Filter: Only add if it matches our security keywords
                if (securityKeywords.Any(k => message.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    list.Add(new AuditLogEvent
                    {
                        LogDate = Convert.ToDateTime(row["LogDate"]),
                        ProcessInfo = row["ProcessInfo"].ToString() ?? "",
                        Message = message
                    });
                }
            }
            return list.OrderByDescending(x => x.LogDate).ToList();
        }

        // Services/MetadataService.cs (Modified GetMaskingCandidates method)

        public List<MaskingCandidate> GetMaskingCandidates()
        {
            var list = new List<MaskingCandidate>();
            // Updated filter to exclude email and address
            string sql = @"
                SELECT 
                    SCHEMA_NAME(t.schema_id) AS SchemaName,
                    t.name AS TableName,
                    c.name AS ColumnName,
                    ty.name AS DataType,
                    ISNULL(mc.is_masked, 0) AS IsMasked,
                    ISNULL(mc.masking_function, 'NONE') AS MaskingFunction
                FROM sys.columns c
                JOIN sys.tables t ON c.object_id = t.object_id
                JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                LEFT JOIN sys.masked_columns mc ON c.object_id = mc.object_id AND c.column_id = mc.column_id
                WHERE c.name LIKE '%phone%' 
                   OR c.name LIKE '%sec_no%' 
                   OR c.name LIKE '%passw%' 
                   OR c.name LIKE '%credit%' 
                   OR mc.is_masked = 1"; // Keep columns that are already masked regardless of name

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new MaskingCandidate
                {
                    SchemaName = row["SchemaName"].ToString() ?? "",
                    TableName = row["TableName"].ToString() ?? "",
                    ColumnName = row["ColumnName"].ToString() ?? "",
                    DataType = row["DataType"].ToString() ?? "",
                    IsMasked = Convert.ToBoolean(row["IsMasked"]),
                    MaskingFunction = row["MaskingFunction"]?.ToString() ?? "NONE"
                });
            }
            return list;
        }

        public ServerInfoDetail GetServerInformation()
        {
            string sql = @"
        SELECT 
            @@SERVERNAME AS ServerName,
            @@VERSION AS FullVersion,
            SERVERPROPERTY('Edition') AS Edition,
            SERVERPROPERTY('ProductLevel') AS ProductLevel,
            cpu_count AS CpuCount,
            (physical_memory_kb / 1024 / 1024) AS PhysicalRamGB,
            sqlserver_start_time AS StartTime
        FROM sys.dm_os_sys_info";

            var dt = SqlConnectionManager.ExecuteQuery(sql);
            if (dt == null || dt.Rows.Count == 0) return null;

            var row = dt.Rows[0];

            return new ServerInfoDetail
            {
                ServerName = row["ServerName"].ToString() ?? "Unknown",
                // We take the first part of the version string to keep it clean
                Version = row["FullVersion"].ToString()?.Split('\n')[0] ?? "N/A",
                Edition = row["Edition"].ToString() ?? "N/A",
                Level = row["ProductLevel"].ToString() ?? "N/A",
                CpuCount = Convert.ToInt32(row["CpuCount"]),
                PhysicalRamGB = Convert.ToInt32(row["PhysicalRamGB"]),

                // FIX: Assign the StartTime here. 
                // DO NOT try to assign UptimeDisplay anymore.
                StartTime = Convert.ToDateTime(row["StartTime"])
            };
        }

        public List<SchemaDifference> GetBaseSchema(string connectionString)
        {
            var list = new List<SchemaDifference>();
            // Querying system tables to get Object and Column metadata
            string sql = @"
                SELECT 
                    s.name + '.' + t.name AS ObjectName,
                    c.name AS ColumnName,
                    tp.name + 
                    CASE 
                        WHEN tp.name IN ('varchar', 'nvarchar', 'char', 'nchar') THEN '(' + CAST(c.max_length AS VARCHAR) + ')'
                        WHEN tp.name IN ('decimal', 'numeric') THEN '(' + CAST(c.precision AS VARCHAR) + ',' + CAST(c.scale AS VARCHAR) + ')'
                        ELSE ''
                    END AS DataType
                FROM sys.tables t
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                JOIN sys.columns c ON t.object_id = c.object_id
                JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                ORDER BY ObjectName, c.column_id";

            try
            {
                // Use a temporary manager or manual connection for the target
                var dt = SqlConnectionManager.ExecuteQueryOnSpecificConnection(sql, connectionString);
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    list.Add(new SchemaDifference
                    {
                        ObjectName = row["ObjectName"].ToString(),
                        ColumnName = row["ColumnName"].ToString(),
                        DataType = row["DataType"].ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                list.Add(new SchemaDifference { DifferenceType = "Error: " + ex.Message });
            }
            return list;
        }

        public List<SqlAgentJob> GetSqlAgentJobs()
        {
            var list = new List<SqlAgentJob>();
            // Using the verified SQL from above
            string sql = @"
                SELECT j.name AS JobName, j.enabled AS IsEnabled,
                ISNULL(CASE jh.run_status WHEN 0 THEN 'FAILED' WHEN 1 THEN 'SUCCEEDED' WHEN 2 THEN 'RETRY' WHEN 3 THEN 'CANCELED' ELSE 'NEVER RUN' END, 'NEVER RUN') AS LastRunOutcome,
                CASE WHEN jh.run_date IS NOT NULL THEN msdb.dbo.agent_datetime(jh.run_date, jh.run_time) ELSE NULL END AS LastRunDate,
                CASE WHEN js.next_run_date > 0 THEN msdb.dbo.agent_datetime(js.next_run_date, js.next_run_time) ELSE NULL END AS NextRunDate,
                ISNULL(STUFF(STUFF(RIGHT('000000' + CAST(jh.run_duration AS VARCHAR(6)), 6), 3, 0, ':'), 6, 0, ':'), '00:00:00') AS LastRunDuration
                FROM msdb.dbo.sysjobs j
                LEFT JOIN msdb.dbo.sysjobschedules js ON j.job_id = js.job_id
                LEFT JOIN (
                    SELECT job_id, run_status, run_date, run_time, run_duration,
                    ROW_NUMBER() OVER (PARTITION BY job_id ORDER BY run_date DESC, run_time DESC) as rank
                    FROM msdb.dbo.sysjobhistory WHERE step_id = 0
                ) jh ON j.job_id = jh.job_id AND jh.rank = 1";

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new SqlAgentJob
                {
                    JobName = row["JobName"].ToString() ?? "",
                    IsEnabled = Convert.ToBoolean(row["IsEnabled"]),
                    LastRunOutcome = row["LastRunOutcome"].ToString(),
                    LastRunDate = row["LastRunDate"] != DBNull.Value ? Convert.ToDateTime(row["LastRunDate"]) : (DateTime?)null,
                    NextRunDate = row["NextRunDate"] != DBNull.Value ? Convert.ToDateTime(row["NextRunDate"]) : (DateTime?)null,
                    LastRunDuration = row["LastRunDuration"].ToString()
                });
            }
            return list;
        }

        public List<BackupHistory> GetBackupHistory()
        {
            var list = new List<BackupHistory>();
            string sql = @"
                SELECT TOP 20
                    s.database_name,
                    s.backup_finish_date AS BackupDate,
                    s.user_name AS UserName,
                    CAST(s.backup_size / 1048576.0 AS DECIMAL(10,2)) AS SizeMB,
                    CASE s.type 
                        WHEN 'D' THEN 'FULL' 
                        WHEN 'I' THEN 'DIFF' 
                        WHEN 'L' THEN 'LOG' 
                    END AS BackupType,
                    m.physical_device_name AS DeviceName
                FROM msdb.dbo.backupset s
                JOIN msdb.dbo.backupmediafamily m ON s.media_set_id = m.media_set_id
                ORDER BY s.backup_finish_date DESC";

            System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new BackupHistory
                {
                    DatabaseName = row["database_name"].ToString() ?? "",
                    BackupDate = Convert.ToDateTime(row["BackupDate"]),
                    UserName = row["UserName"].ToString() ?? "N/A",
                    // FIX CS0266: Explicitly cast to decimal
                    SizeMB = row["SizeMB"] != DBNull.Value ? (decimal)row["SizeMB"] : 0,
                    BackupType = row["BackupType"].ToString() ?? "",
                    DeviceName = row["DeviceName"].ToString() ?? ""
                });
            }
            return list;
        }

        public List<SqlConfigSetting> GetConfigurableSettings()
        {
            var list = new List<SqlConfigSetting>();
            // We force advanced options so we can see Max Memory and MAXDOP
            string sql = @"
                EXEC sp_configure 'show advanced options', 1;
                RECONFIGURE;
                SELECT name, CAST(value_in_use AS VARCHAR) as value, description 
                FROM sys.configurations ORDER BY name";

            var dt = SqlConnectionManager.ExecuteQuery(sql);
            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new SqlConfigSetting
                {
                    Name = row["name"].ToString(),
                    Value = row["value"].ToString(),
                    Description = row["description"].ToString()
                });
            }
            return list;
        }

        public List<DatabaseProperty> GetDatabaseConfiguration()
        {
            var list = new List<DatabaseProperty>();
            // Pulls critical engine settings for the CURRENT database
            string sql = @"
                SELECT 'Recovery Model' as Name, recovery_model_desc as CurrentValue FROM sys.databases WHERE name = DB_NAME()
                UNION ALL
                SELECT 'Auto Close', CASE WHEN is_auto_close_on = 1 THEN 'ON (Warning: Slow)' ELSE 'OFF (Healthy)' END FROM sys.databases WHERE name = DB_NAME()
                UNION ALL
                SELECT 'Auto Shrink', CASE WHEN is_auto_shrink_on = 1 THEN 'ON (Warning: Fragmentation)' ELSE 'OFF (Healthy)' END FROM sys.databases WHERE name = DB_NAME()
                UNION ALL
                SELECT 'Page Verification', page_verify_option_desc FROM sys.databases WHERE name = DB_NAME()";

            var dt = SqlConnectionManager.ExecuteQuery(sql);
            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new DatabaseProperty
                {
                    Name = row["Name"].ToString(),
                    CurrentValue = row["CurrentValue"].ToString()
                });
            }
            return list;
        }

        public List<SqlSnippet> GetSnippetLibrary()
        {
            return new List<SqlSnippet>
    {
        new SqlSnippet
        {
            Title = "Find Large Tables (> 1GB)",
            Category = "Maintenance",
            Description = "Identifies the top 10 largest user tables in the database by space used, including indexes.",
            Code = @"SELECT TOP 10 
                    t.name AS TableName, 
                    SUM(p.reserved_page_count) * 8.0 / 1024 AS Reserved_MB
                FROM sys.tables t 
                JOIN sys.partitions p ON t.object_id = p.object_id
                GROUP BY t.name
                ORDER BY Reserved_MB DESC;"
        },
        new SqlSnippet
        {
            Title = "View Query Plan Cache Usage",
            Category = "Performance",
            Description = "Shows total memory consumed by query plans in the instance.",
            Code = @"SELECT SUM(size_in_bytes) / 1024 / 1024 AS TotalCacheMB 
                FROM sys.dm_exec_cached_plans;"
        },
        new SqlSnippet
        {
            Title = "Find All Blocking Sessions",
            Category = "Performance",
            Description = "Quickly identifies the head blocker and all sessions currently blocked by it.",
            Code = @"SELECT 
                    t1.session_id AS Blocked_Session,
                    t2.session_id AS Head_Blocker,
                    t2.login_name,
                    t2.host_name,
                    t2.program_name,
                    t2.status
                FROM sys.dm_exec_requests t1
                INNER JOIN sys.dm_exec_sessions t2 ON t1.blocking_session_id = t2.session_id
                WHERE t1.blocking_session_id != 0;"
        },
        new SqlSnippet
        {
            Title = "Top 20 Expensive Queries (by CPU)",
            Category = "Performance",
            Description = "Retrieves the top 20 most CPU-intensive queries from the plan cache.",
            Code = @"SELECT TOP 20
                    qs.execution_count,
                    qs.total_worker_time AS Total_CPU_Time,
                    qs.total_worker_time/qs.execution_count AS Avg_CPU_Time,
                    SUBSTRING(st.text, (qs.statement_start_offset/2) + 1,
                        ((CASE qs.statement_end_offset
                            WHEN -1 THEN DATALENGTH(st.text)
                            ELSE qs.statement_end_offset
                        END - qs.statement_start_offset)/2) + 1) AS Statement_Text
                FROM sys.dm_exec_query_stats AS qs
                CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
                ORDER BY qs.total_worker_time DESC;"
        },
        new SqlSnippet
        {
            Title = "Find Highly Fragmented Indexes",
            Category = "Maintenance",
            Description = "Lists indexes with high fragmentation levels (over 30%) that should be rebuilt.",
            Code = @"SELECT 
                    OBJECT_NAME(ips.object_id) AS TableName,
                    i.name AS IndexName,
                    ips.avg_fragmentation_in_percent,
                    ips.page_count
                FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') AS ips
                INNER JOIN sys.indexes AS i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
                WHERE ips.avg_fragmentation_in_percent > 30.0 AND ips.index_type_desc = 'NONCLUSTERED'
                ORDER BY ips.avg_fragmentation_in_percent DESC;"
        },
        new SqlSnippet
        {
            Title = "Database Free Space Report",
            Category = "Maintenance",
            Description = "Reports on space utilization for data and log files.",
            Code = @"SELECT 
                    name AS FileName, 
                    size/128.0 AS CurrentSizeMB, 
                    size/128.0 - CAST(FILEPROPERTY(name, 'SpaceUsed') AS INT)/128.0 AS FreeSpaceMB, 
                    physical_name AS FileLocation
                FROM sys.database_files;"
        },
        new SqlSnippet
        {
            Title = "Audit Failed Login Attempts",
            Category = "Security",
            Description = "Queries the SQL Server Error Log to check for recent failed logins.",
            Code = @"EXEC sys.xp_readerrorlog 0, 1, N'Login failed';"
        },
        new SqlSnippet
        {
            Title = "Check Database Role Membership",
            Category = "Security",
            Description = "Shows all users and the database roles they belong to.",
            Code = @"SELECT 
                    dp.name AS DatabaseRole,
                    CASE 
                        WHEN dp.principal_id < 0 THEN NULL
                        ELSE member.name
                    END AS MemberName,
                    member.type_desc AS MemberType
                FROM sys.database_role_members AS drm
                JOIN sys.database_principals AS dp ON drm.role_principal_id = dp.principal_id
                JOIN sys.database_principals AS member ON drm.member_principal_id = member.principal_id
                ORDER BY DatabaseRole, MemberName;"
        },
        new SqlSnippet
        {
            Title = "Find Missing Indexes",
            Category = "Performance",
            Description = "Uses DMVs to find indexes that the SQL optimizer thinks would improve performance.",
            Code = @"SELECT TOP 20
                    mid.statement AS [Database.Schema.Table],
                    migs.avg_user_impact AS [Potential Benefit %],
                    mid.equality_columns AS [Equality Columns],
                    mid.inequality_columns AS [Inequality Columns],
                    mid.included_columns AS [Include Columns]
                FROM sys.dm_db_missing_index_groups mig
                INNER JOIN sys.dm_db_missing_index_group_stats migs ON migs.group_handle = mig.index_group_handle
                INNER JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
                ORDER BY migs.avg_user_impact DESC;"
        },
        new SqlSnippet
        {
            Title = "Kill All Database Connections",
            Category = "Maintenance",
            Description = "Drops all connections and sets DB to SINGLE_USER. Use with CAUTION.",
            Code = @"DECLARE @DatabaseName nvarchar(50) = 'YourDatabaseName'
                DECLARE @SQL nvarchar(max) = ''

                SELECT @SQL += 'KILL ' + CAST(session_id AS varchar(5)) + ';'
                FROM sys.dm_exec_sessions
                WHERE database_id = DB_ID(@DatabaseName) AND session_id <> @@SPID

                EXEC sp_executesql @SQL;
                EXEC('ALTER DATABASE [' + @DatabaseName + '] SET SINGLE_USER WITH ROLLBACK IMMEDIATE');"
        },
        new SqlSnippet
        {
            Title = "Identity Column Exhaustion Check",
            Category = "Maintenance",
            Description = "Finds ID columns approaching the limit of their data type (e.g., INT approaching 2 billion).",
            Code = @"SELECT 
                    QUOTENAME(SCHEMA_NAME(t.schema_id)) + '.' + QUOTENAME(t.name) AS TableName,
                    c.name AS ColumnName,
                    ic.seed_value,
                    ic.last_value,
                    tp.name AS DataType
                FROM sys.identity_columns ic
                INNER JOIN sys.tables t ON ic.object_id = t.object_id
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                INNER JOIN sys.types tp ON c.system_type_id = tp.system_type_id
                WHERE ic.last_value IS NOT NULL;"
        }
    };
        }

        // CRITICAL FIX #3: GetQueryExecutionPlan() - Query Execution Restriction
        public string GetQueryExecutionPlan(string query)
        {
            // 1. SECURITY VALIDATION (Keep these!)
            if (string.IsNullOrWhiteSpace(query)) return "ERROR: Query cannot be empty.";

            string trimmedQuery = query.Trim();
            if (!trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                return "ERROR: Only SELECT statements are allowed for execution plan analysis.";

            if (query.Length > 10000) return "ERROR: Query is too long.";

            string? connectionString = SqlConnectionManager.GetCurrentConnectionString();
            if (string.IsNullOrEmpty(connectionString)) throw new InvalidOperationException("Connection not established.");

            try
            {
                // Use 'using' to ensure the connection is ALWAYS closed, even on error
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    connection.Open();
                    int timeout = 30;

                    // Turn ON Showplan
                    using (var cmdOn = new SqlCommand("SET SHOWPLAN_XML ON;", connection)) { cmdOn.CommandTimeout = timeout; cmdOn.ExecuteNonQuery(); }

                    string planXml = "";
                    using (var cmdQuery = new SqlCommand(query, connection))
                    {
                        cmdQuery.CommandTimeout = timeout;
                        // ExecuteXmlReader is the most memory-efficient way to get large plans
                        using (var reader = cmdQuery.ExecuteXmlReader())
                        {
                            if (reader.Read()) planXml = reader.ReadOuterXml();
                        }
                    }

                    // Turn OFF Showplan (Crucial!)
                    using (var cmdOff = new SqlCommand("SET SHOWPLAN_XML OFF;", connection)) { cmdOff.CommandTimeout = timeout; cmdOff.ExecuteNonQuery(); }

                    return !string.IsNullOrEmpty(planXml) ? planXml : "No execution plan generated.";
                }
            }
            catch (SqlException ex)
            {
                // Log locally for you, but keep user message generic for security
                System.Diagnostics.Debug.WriteLine($"SQL Error: {ex.Message}");
                return "ERROR: Could not retrieve plan. Verify query syntax or permissions.";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public List<AgReplicaStatus> GetAvailabilityGroupStatus()
        {
            var list = new List<AgReplicaStatus>();

            // 1. Try Always On (Availability Groups)
            string agSql = @"
                IF EXISTS (SELECT * FROM sys.dm_hadr_availability_replica_states)
                BEGIN
                    SELECT 
                        ag.name AS AGName,
                        ar.replica_server_name AS ReplicaServerName,
                        rs.role_desc AS Role,
                        ar.failover_mode_desc AS FailoverMode,
                        drs.synchronization_state_desc AS SynchronizationState,
                        rs.operational_state_desc AS OperationalState
                    FROM sys.availability_groups ag
                    JOIN sys.availability_replicas ar ON ag.group_id = ar.group_id
                    JOIN sys.dm_hadr_availability_replica_states rs ON ar.replica_id = rs.replica_id
                    JOIN sys.dm_hadr_database_replica_states drs ON rs.replica_id = drs.replica_id
                    GROUP BY ag.name, ar.replica_server_name, rs.role_desc, ar.failover_mode_desc, 
                             drs.synchronization_state_desc, rs.operational_state_desc
                END";

            // 2. Fallback: Check Database Mirroring
            string mirrorSql = @"
                SELECT 
                    DB_NAME(database_id) AS AGName,
                    mirroring_partner_instance AS ReplicaServerName,
                    mirroring_role_desc AS Role,
                    mirroring_safety_level_desc AS FailoverMode,
                    mirroring_state_desc AS SynchronizationState,
                    'HEALTHY' AS OperationalState
                FROM sys.database_mirroring
                WHERE mirroring_guid IS NOT NULL";

            try
            {
                var dt = SqlConnectionManager.ExecuteQuery(agSql);

                // If AlwaysOn is empty, try Mirroring
                if (dt.Rows.Count == 0)
                {
                    dt = SqlConnectionManager.ExecuteQuery(mirrorSql);
                }

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    list.Add(new AgReplicaStatus
                    {
                        AGName = row["AGName"].ToString(),
                        ReplicaServerName = row["ReplicaServerName"].ToString() ?? "Standalone",
                        Role = row["Role"].ToString(),
                        FailoverMode = row["FailoverMode"].ToString(),
                        SynchronizationState = row["SynchronizationState"].ToString(),
                        OperationalState = row["OperationalState"].ToString()
                    });
                }
            }
            catch { /* Handle instances where HA features aren't installed */ }

            return list;
        }

        public List<DriveSpaceInfo> GetDriveSpaceReport()
        {
            var list = new List<DriveSpaceInfo>();

            string sql = @"
                SELECT 
                    vs.volume_mount_point AS DriveLetter,
                    MAX(vs.logical_volume_name) AS VolumeName,
                    CAST(MAX(vs.available_bytes) / 1073741824.0 AS DECIMAL(10,2)) AS FreeSpaceGB,
                    CAST(MAX(vs.total_bytes) / 1073741824.0 AS DECIMAL(10,2)) AS TotalSizeGB,
                    COUNT(f.file_id) AS DatabaseFileCount,
                    STUFF((SELECT DISTINCT ', ' + DB_NAME(mf.database_id)
                           FROM sys.master_files mf
                           -- Check if the file path starts with the drive mount point
                           WHERE mf.physical_name LIKE vs.volume_mount_point + '%'
                           FOR XML PATH('')), 1, 2, '') AS DatabaseFiles
                FROM sys.master_files AS f
                CROSS APPLY sys.dm_os_volume_stats(f.database_id, f.file_id) AS vs
                GROUP BY vs.volume_mount_point
                ORDER BY vs.volume_mount_point;";

            try
            {
                System.Data.DataTable dt = SqlConnectionManager.ExecuteQuery(sql);

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    list.Add(new DriveSpaceInfo
                    {
                        DriveLetter = row["DriveLetter"].ToString() ?? "",
                        VolumeName = row["VolumeName"].ToString() ?? "Local Disk",
                        FreeSpaceGB = Convert.ToDouble(row["FreeSpaceGB"]),
                        TotalSizeGB = Convert.ToDouble(row["TotalSizeGB"]),
                        DatabaseFileCount = Convert.ToInt32(row["DatabaseFileCount"]),
                        DatabaseFiles = row["DatabaseFiles"]?.ToString() ?? "None"
                    });
                }
            }
            catch (Exception ex)
            {
                list.Add(new DriveSpaceInfo { DriveLetter = "ERR", DatabaseFiles = ex.Message });
            }

            return list;
        }

        public List<ObjectDependencyDetail> GetTableUsageDetails(string schemaName, string tableName)
        {
            try
            {
                // 1. Retrieve the necessary context
                string? databaseName = SQLAtlas.Data.SqlConnectionManager.CurrentDatabaseName;
                if (string.IsNullOrEmpty(databaseName)) return new List<ObjectDependencyDetail>();

                string qualifiedName = $"[{schemaName}].[{tableName}]";

                // 2. T-SQL: Get the list of dependent objects
                // This query relies only on sys.sql_expression_dependencies to list the relationships.
                string sql = $@"
            USE [{databaseName}]; 

            DECLARE @TargetObjectId INT = OBJECT_ID(@QualifiedName);

            SELECT
                QUOTENAME(s.name) + '.' + QUOTENAME(o.name) AS ObjectName,
                o.type_desc AS ObjectType
            FROM [{databaseName}].sys.sql_expression_dependencies dep 
            INNER JOIN [{databaseName}].sys.objects o ON dep.referencing_id = o.object_id
            INNER JOIN [{databaseName}].sys.schemas s ON o.schema_id = s.schema_id
            WHERE 
                dep.referenced_id = @TargetObjectId
                AND o.type IN ('P', 'V', 'FN', 'IF', 'TF') -- Procedures, Views, Functions
            ORDER BY o.type_desc, ObjectName;";

                // 3. Set parameters for the query
                var parameters = new Dictionary<string, object>
        {
            { "@QualifiedName", qualifiedName }
        };

                // 4. Execute the query to get the list of dependent objects
                DataTable dt = SQLAtlas.Data.SqlConnectionManager.ExecuteQuery(sql, parameters);
                var dependencyList = new List<ObjectDependencyDetail>();

                if (dt is not null)
                {
                    // 5. Loop through each dependent object to get its specific CRUD flags
                    foreach (DataRow row in dt.Rows)
                    {
                        string objName = row["ObjectName"]?.ToString() ?? "N/A";
                        string objType = row["ObjectType"]?.ToString() ?? "N/A";

                        // CRITICAL: Call helper method to get R/W flags for this specific dependent object
                        // The objName comes back fully quoted (e.g., [dbo].[usp_name]), we pass that to the helper.
                        (string read, string write, string reference) = GetObjectCrudDetails(databaseName, objName, qualifiedName);

                        dependencyList.Add(new ObjectDependencyDetail
                        {
                            ObjectName = objName,
                            ObjectType = objType,
                            ReadStatus = read,
                            WriteStatus = write,
                            ReferenceType = reference
                        });
                    }
                }
                return dependencyList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dependency Lookup Error: {ex.Message}");
                return new List<ObjectDependencyDetail>
        {
            new ObjectDependencyDetail { ObjectName = $"ERROR: Dependency service failed. {ex.Message}", ObjectType = "N/A" }
        };
            }
        }

        private (string Read, string Write, string Reference) GetObjectCrudDetails(string databaseName, string dependentObjectName, string targetTableName)
        {
            try
            {
                string unquotedDependentName = dependentObjectName.Replace("[", "").Replace("]", "");

                // T-SQL uses the DMF that contains the boolean flags for R/W actions.
                string sql = $@"
                USE [{databaseName}];
    
                SELECT 
                    ISNULL(CAST(ref.is_selected AS INT), 0) AS is_selected,
                    ISNULL(CAST(ref.is_inserted AS INT), 0) AS is_inserted,
                    ISNULL(CAST(ref.is_updated AS INT), 0) AS is_updated,
                    ISNULL(CAST(ref.is_deleted AS INT), 0) AS is_deleted
                FROM [{databaseName}].sys.dm_sql_referenced_entities (
                    @DependentObjectName, 'OBJECT'
                ) AS ref
                WHERE 
                    ref.referenced_name = OBJECT_NAME(OBJECT_ID(@TargetTableName))
                    AND ref.referenced_schema_name = OBJECT_SCHEMA_NAME(OBJECT_ID(@TargetTableName));";

                var parameters = new Dictionary<string, object>
                {
                    // FIX: Pass the cleaned, unquoted name string to the DMF parameter
                    { "@DependentObjectName", unquotedDependentName },
                    { "@TargetTableName", targetTableName }
                };

                DataTable dt = SQLAtlas.Data.SqlConnectionManager.ExecuteQuery(sql, parameters);

                // CRITICAL FIX: Check if dt is null or has no rows
                if (dt is null || dt.Rows.Count == 0)
                {
                    return ("N/A", "N/A", "UNKNOWN");
                }

                DataRow row = dt.Rows[0];

                // --- CRITICAL FIX: Use DataRow.Field<int?> for reliable type casting ---
                // This safely retrieves SQL's 1, 0, or NULL values as a nullable C# integer.
                int? isSelected = row.Field<int?>("is_selected");
                int? isInserted = row.Field<int?>("is_inserted");
                int? isUpdated = row.Field<int?>("is_updated");
                int? isDeleted = row.Field<int?>("is_deleted");

                // --- Determine Read Status ---
                // Read status is true if the nullable integer is present AND its value is 1.
                string read = (isSelected.HasValue && isSelected.Value == 1) ? "READ" : "N/A";

                // --- Determine Write Action Status (Prioritizing one action) ---
                string writeStatus = "N/A";

                if (isInserted.HasValue && isInserted.Value == 1)
                {
                    writeStatus = "INSERT";
                }
                else if (isUpdated.HasValue && isUpdated.Value == 1)
                {
                    writeStatus = "UPDATE";
                }
                else if (isDeleted.HasValue && isDeleted.Value == 1)
                {
                    writeStatus = "DELETE";
                }

                // --- Determine Final Reference Type ---
                string referenceType;

                if (writeStatus != "N/A")
                {
                    referenceType = $"WRITE ({writeStatus})";
                }
                else if (read == "READ")
                {
                    referenceType = "READ Only";
                }
                else
                {
                    // If neither R nor W flags are true (e.g., object is mentioned but not accessed)
                    referenceType = "UNKNOWN";
                }

                return (read, writeStatus, referenceType);
            }
            catch (Exception ex)
            {
                // Log the error and return the exception message for debugging in the UI
                System.Diagnostics.Debug.WriteLine($"CRUD Details Failed for {dependentObjectName}: {ex.Message}");

                // Return UNKNOWN/N/A status along with the error message
                return ("N/A", $"ERROR: {ex.Message}", "UNKNOWN");
            }
        }

        // This is for SchemaExplorer - Primary and Unique Keys
        public List<KeyDetail> GetPrimaryAndUniqueKeys(string schemaName, string tableName)
        {
            try
            {
                // T-SQL to find PRIMARY KEY and UNIQUE constraints on the given table
                string sql = @"
            SELECT 
                kc.name AS ConstraintName,
                kc.type_desc AS KeyType,
                STUFF((
                    SELECT ', ' + c.name 
                    FROM sys.index_columns ic
                    JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                    WHERE ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
                    ORDER BY ic.key_ordinal
                    FOR XML PATH('')
                ), 1, 2, '') AS Columns
            FROM sys.key_constraints kc
            WHERE kc.parent_object_id = OBJECT_ID(@QualifiedName)
              AND kc.type IN ('PK', 'UQ');"; // PK: Primary Key, UQ: Unique Constraint

                var parameters = new Dictionary<string, object>
        {
            { "@QualifiedName", $"[{schemaName}].[{tableName}]" }
        };

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql, parameters);
                var keyDetails = new List<KeyDetail>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        keyDetails.Add(new KeyDetail
                        {
                            ConstraintName = row["ConstraintName"]?.ToString() ?? "N/A",
                            KeyType = row["KeyType"]?.ToString() ?? "N/A",
                            Columns = row["Columns"]?.ToString() ?? "N/A",
                            ReferencesTable = "N/A"
                        });
                    }
                }
                return keyDetails;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PK/UQ Key Error: {ex.Message}");
                return new List<KeyDetail>();
            }
        }

        // This is for SchemaExplorer - Foreign Keys
        public List<KeyDetail> GetForeignKeys(string schemaName, string tableName)
        {
            try
            {
                // T-SQL to find all FOREIGN KEY constraints where the given table is the PARENT (defines the FK)
                string sql = @"
            SELECT
                fk.name AS ConstraintName,
                'FOREIGN KEY' AS KeyType,
                STUFF((
                    SELECT ', ' + pc.name 
                    FROM sys.foreign_key_columns fkc
                    JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
                    WHERE fkc.constraint_object_id = fk.object_id
                    FOR XML PATH('')
                ), 1, 2, '') AS Columns,
                OBJECT_NAME(fk.referenced_object_id) AS ReferencesTable
            FROM sys.foreign_keys fk
            WHERE fk.parent_object_id = OBJECT_ID(@QualifiedName);";

                var parameters = new Dictionary<string, object>
        {
            { "@QualifiedName", $"[{schemaName}].[{tableName}]" }
        };

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql, parameters);
                var keyDetails = new List<KeyDetail>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        keyDetails.Add(new KeyDetail
                        {
                            ConstraintName = row["ConstraintName"]?.ToString() ?? "N/A",
                            KeyType = "FOREIGN KEY",
                            Columns = row["Columns"]?.ToString() ?? "N/A",
                            ReferencesTable = row["ReferencesTable"]?.ToString() ?? "N/A"
                        });
                    }
                }
                return keyDetails;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FK Key Error: {ex.Message}");
                return new List<KeyDetail>();
            }
        }

        // This is for SchemaExplorer - Index Fragmentation Details
        public List<IndexDetail> GetTableIndexes(string schemaName, string tableName)
        {
            var list = new List<IndexDetail>();

            // FAST Metadata-only query (No fragmentation calculation)
            string sql = @"
        SELECT 
            i.name AS IndexName,
            i.type_desc AS IndexType,
            ISNULL(STUFF((SELECT ', ' + c.name
                   FROM sys.index_columns ic
                   JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                   WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                   ORDER BY ic.key_ordinal
                   FOR XML PATH('')), 1, 2, ''), '') AS KeyColumns,
            ISNULL(STUFF((SELECT ', ' + c.name
                   FROM sys.index_columns ic
                   JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                   WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                   ORDER BY ic.key_ordinal
                   FOR XML PATH('')), 1, 2, ''), '') AS IncludedColumns
        FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(@FullTableName)
        AND i.name IS NOT NULL;";

            try
            {
                // FIX: Retrieve the connection string from your central SqlConnectionManager
                string? connString = SQLAtlas.Data.SqlConnectionManager.GetCurrentConnectionString();

                if (string.IsNullOrEmpty(connString))
                {
                    return list;
                }

                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connString))
                {
                    conn.Open();
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@FullTableName", $"[{schemaName}].[{tableName}]");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new IndexDetail
                                {
                                    IndexName = reader["IndexName"]?.ToString() ?? "N/A",
                                    IndexType = reader["IndexType"]?.ToString() ?? "N/A",
                                    KeyColumns = reader["KeyColumns"]?.ToString() ?? "N/A",
                                    IncludedColumns = reader["IncludedColumns"]?.ToString() ?? "",
                                    FragmentationPercent = 0, // Bypass slow calculation
                                    PageCount = 0
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MetadataService Error: {ex.Message}");
            }

            return list;
        }

        public void KillSession(int sessionId)
        {
            // We use a formatted string here because KILL doesn't support parameters like @id
            string sql = $"KILL {sessionId}";

            try
            {
                SqlConnectionManager.ExecuteNonQuery(sql);
            }
            catch (Exception ex)
            {
                // This will be caught by your UI and shown in the MessageBox
                throw new Exception($"SQL Server refused to kill session {sessionId}. It might be a system process or already terminating. Details: {ex.Message}");
            }
        }

    }
}