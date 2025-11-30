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
                    var obj = new DatabaseObject
                    {
                        // CRITICAL: Trim and standardize names/types for reliable lookup
                        Name = row["name"]?.ToString()?.Trim() ?? string.Empty,
                        Type = row["type"]?.ToString()?.Trim().ToUpperInvariant() ?? string.Empty,
                        TypeDescription = row["type_desc"]?.ToString()?.Trim() ?? string.Empty,
                        SchemaName = row["SchemaName"]?.ToString()?.Trim() ?? string.Empty
                    };

                    string groupKey = obj.TypeDescription;
                    if (!groupedObjects.ContainsKey(groupKey))
                    {
                        groupedObjects[groupKey] = new List<DatabaseObject>();
                    }
                    groupedObjects[groupKey].Add(obj);
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

        public List<ForeignKeyDetails> GetForeignKeysBetween(string table1Name, string table2Name)
        {
            try
            {
                string sql = @"
                    SELECT
                        fk.name AS ConstraintName,
                        OBJECT_NAME(fk.parent_object_id) AS ParentTable,
                        pc.name AS ParentColumn,
                        OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
                        rc.name AS ReferencedColumn
                    FROM 
                        sys.foreign_keys fk
                    INNER JOIN 
                        sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN 
                        sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
                    INNER JOIN 
                        sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
                    WHERE
                        (OBJECT_NAME(fk.parent_object_id) = @Table1Name AND OBJECT_NAME(fk.referenced_object_id) = @Table2Name)
                        OR
                        (OBJECT_NAME(fk.parent_object_id) = @Table2Name AND OBJECT_NAME(fk.referenced_object_id) = @Table1Name);";

                var parameters = new Dictionary<string, object>
                {
                    { "@Table1Name", table1Name },
                    { "@Table2Name", table2Name }
                };

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql, parameters);
                var fks = new List<ForeignKeyDetails>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        fks.Add(new ForeignKeyDetails
                        {
                            ConstraintName = row["ConstraintName"]?.ToString() ?? string.Empty,
                            ParentTable = row["ParentTable"]?.ToString() ?? string.Empty,
                            ParentColumn = row["ParentColumn"]?.ToString() ?? string.Empty,
                            ReferencedTable = row["ReferencedTable"]?.ToString() ?? string.Empty,
                            ReferencedColumn = row["ReferencedColumn"]?.ToString() ?? string.Empty,
                        });
                    }
                }
                return fks;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Foreign Key Data Processing Error: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<ForeignKeyDetails>();
            }
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
                SELECT 
                    s.session_id AS SessionID, 
                    s.login_name AS LoginName, 
                    s.host_name AS HostName,
                    s.login_time AS LoginTime,  
                    r.status AS Status, 
                    r.command AS Command, 
                    r.start_time AS StartTime
                FROM 
                    sys.dm_exec_sessions s
                LEFT JOIN 
                    sys.dm_exec_requests r ON s.session_id = r.session_id
                WHERE 
                    s.is_user_process = 1
                    AND s.session_id <> @@SPID 
                ORDER BY 
                    r.start_time DESC;";

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
                -- Calculate duration in seconds
                DATEDIFF(SECOND, r.start_time, GETDATE()) AS DurationSeconds,
                t.text AS SqlStatement
            FROM sys.dm_exec_requests r
            CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
            WHERE 
                r.session_id <> @@SPID -- Exclude the current session
                AND r.status <> 'background' -- Exclude background tasks
                AND r.command NOT IN ('AWAITING COMMAND')
            ORDER BY DurationSeconds DESC;";

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
            try
            {
                // T-SQL to retrieve missing index details and performance impact metrics
                string sql = @"
                    SELECT TOP 10
                        DB_NAME(d.database_id) AS DatabaseName,
                        s.name AS SchemaName,
                        OBJECT_NAME(d.object_id) AS TableName,
                        d.equality_columns AS EqualityColumns,
                        d.inequality_columns AS InequalityColumns,
                        d.included_columns AS IncludedColumns,
                        -- Calculates the score (impact * searches * updates) and orders by it
                        -- The average user impact is the estimated percentage improvement
                        gs.avg_user_impact AS AvgUserImpact
                    FROM sys.dm_db_missing_index_details d
                    INNER JOIN sys.dm_db_missing_index_groups g ON d.index_handle = g.index_handle
                    INNER JOIN sys.dm_db_missing_index_group_stats gs ON g.index_group_handle = gs.group_handle
                    INNER JOIN sys.schemas s ON d.schema_id = s.schema_id
                    WHERE d.database_id = DB_ID() -- Filter to the current database
                    ORDER BY AvgUserImpact DESC;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var indexes = new List<MissingIndex>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        indexes.Add(new MissingIndex
                        {
                            DatabaseName = row["DatabaseName"]?.ToString() ?? "N/A",
                            SchemaName = row["SchemaName"]?.ToString() ?? "N/A",
                            TableName = row["TableName"]?.ToString() ?? "N/A",
                            EqualityColumns = row["EqualityColumns"]?.ToString() ?? "N/A",
                            InequalityColumns = row["InequalityColumns"]?.ToString() ?? "N/A",
                            IncludedColumns = row["IncludedColumns"]?.ToString() ?? "N/A",
                            // Safely convert the impact score
                            AvgUserImpact = (row["AvgUserImpact"] != DBNull.Value) ? Convert.ToDouble(row["AvgUserImpact"]) : 0.0
                        });
                    }
                }
                return indexes;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Missing Indexes Access Error: {ex.Message}", "DMV Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<MissingIndex>();
            }
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

        public List<CachedQueryMetric> GetTopExpensiveQueries()
        {
            try
            {
                // T-SQL to find the TOP 10 most expensive queries by Total Logical Reads (I/O)
                string sql = @"
            SELECT TOP 10
                qs.execution_count AS ExecutionCount,
                qs.total_worker_time AS TotalCPUTime,
                qs.total_logical_reads AS TotalLogicalReads,
                qs.total_worker_time / qs.execution_count AS AvgCPUTime,
                SUBSTRING(st.text, (qs.statement_start_offset/2)+1, 
                    ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(st.text) 
                      ELSE qs.statement_end_offset END - qs.statement_start_offset)/2)+1) AS QueryStatement
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
            ORDER BY TotalLogicalReads DESC;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var metrics = new List<CachedQueryMetric>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        // Safely retrieve the query statement object (can be DBNull)
                        object queryStatementObj = row["QueryStatement"];

                        // Convert to string, or default to string.Empty if DBNull/Null is found.
                        string fullQueryStatement = (queryStatementObj is DBNull)
                                                    ? string.Empty
                                                    : queryStatementObj.ToString() ?? string.Empty;

                        // Trim the whitespace from the resulting string.
                        string trimmedStatement = fullQueryStatement.Trim();

                        metrics.Add(new CachedQueryMetric
                        {
                            ExecutionCount = Convert.ToInt64(row["ExecutionCount"]),
                            TotalCPUTimeMS = (Convert.ToInt64(row["TotalCPUTime"]) / 1000).ToString("N0"),
                            TotalLogicalReads = Convert.ToInt64(row["TotalLogicalReads"]).ToString("N0"),
                            AvgCPUTimeMS = (Convert.ToInt64(row["AvgCPUTime"]) / 1000).ToString("N0"),

                            // Assign the safely retrieved and trimmed string
                            QueryStatement = trimmedStatement
                        });
                    }
                }
                return metrics;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Expensive Queries Access Error: {ex.Message}", "Optimization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<CachedQueryMetric>();
            }
        }

        public List<WaitStat> GetTopWaits()
        {
            try
            {
                // T-SQL to find the TOP 10 highest cumulative wait types, excluding noise.
                string sql = @"
            SELECT TOP 10
                wait_type AS WaitType,
                -- Convert milliseconds to seconds
                CAST(waiting_tasks_count * 1.0 * wait_time_ms / 1000.0 AS DECIMAL(18, 2)) AS WaitTimeSeconds,
                waiting_tasks_count AS WaitingTasksCount
            FROM sys.dm_os_wait_stats 
            WHERE 
                -- Exclude common 'noise' waits that are benign or unrelated to performance bottlenecks
                wait_type NOT IN ('SLEEP_TASK', 'WAITFOR', 'SQLTRACE_INCREMENTAL_FLUSH_SLEEP')
                AND wait_time_ms > 0
            ORDER BY WaitTimeSeconds DESC;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var waits = new List<WaitStat>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        waits.Add(new WaitStat
                        {
                            WaitType = row["WaitType"]?.ToString() ?? "N/A",
                            WaitTimeSeconds = Convert.ToInt64(row["WaitTimeSeconds"]),
                            WaitingTasksCount = Convert.ToInt64(row["WaitingTasksCount"])
                        });
                    }
                }
                return waits;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wait Stats Access Error: {ex.Message}", "Optimization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<WaitStat>();
            }
        }

        public List<IndexFragmentation> GetIndexFragmentation()
        {
            try
            {
                string sql = @"
                    SELECT
                        QUOTENAME(s.name) + '.' + QUOTENAME(t.name) AS SchemaTableName,
                        si.name AS IndexName,
                        ips.avg_fragmentation_in_percent,
                        ips.page_count,
                        CASE
                            WHEN ips.avg_fragmentation_in_percent >= 30 AND ips.page_count > 8 THEN 'REBUILD (CRITICAL)'
                            WHEN ips.avg_fragmentation_in_percent > 5 AND ips.page_count > 8 THEN 'REORGANIZE'
                            ELSE 'OK'
                        END AS MaintenanceAction
                    FROM
                        sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'SAMPLED') AS ips
                    INNER JOIN
                        sys.tables AS t ON ips.object_id = t.object_id  -- Ensure table exists
                    INNER JOIN
                        sys.schemas AS s ON t.schema_id = s.schema_id
                    INNER JOIN
                        sys.indexes AS si ON ips.object_id = si.object_id AND ips.index_id = si.index_id
                    WHERE
                        ips.index_id > 0 
                        AND ips.page_count > 8
                    ORDER BY
                        OBJECT_NAME(ips.object_id), ips.avg_fragmentation_in_percent DESC;"; 

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var fragList = new List<IndexFragmentation>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        // Safety check helpers
                        var fragObj = row["avg_fragmentation_in_percent"];
                        var pageObj = row["page_count"];

                        // 1. Safely retrieve numeric values, defaulting to 0.0 or 0
                        double fragmentationPercent = (row["avg_fragmentation_in_percent"] is DBNull)
                                                              ? 0.0
                                                              : Convert.ToDouble(row["avg_fragmentation_in_percent"]);

                        long pageCount = (row["page_count"] is DBNull)
                                                 ? 0
                                                 : Convert.ToInt64(row["page_count"]);

                        string indexName = row["IndexName"]?.ToString() ?? "N/A";

                        // CRITICAL CHECK: Filter out results that have unresolvable names (just in case)
                        if (indexName == "N/A") continue;

                        // 2. Determine action and script (using the new safe variables)
                        string action;
                        string script;
                        string schemaTableName = row["SchemaTableName"]?.ToString() ?? "N/A";

                        if (fragmentationPercent >= 30 && pageCount > 8)
                        {
                            action = "REBUILD (CRITICAL)";
                            script = $"ALTER INDEX [{indexName}] ON {schemaTableName} REBUILD WITH (ONLINE = (ON), SORT_IN_TEMPDB = ON);";
                        }
                        else if (fragmentationPercent > 5 && pageCount > 8)
                        {
                            action = "REORGANIZE";
                            script = $"ALTER INDEX [{indexName}] ON {schemaTableName} REORGANIZE;";
                        }
                        else
                        {
                            action = "OK";
                            script = "N/A";
                        }

                        fragList.Add(new IndexFragmentation
                        {
                            SchemaTableName = schemaTableName,
                            IndexName = indexName,
                            FragmentationPercent = fragmentationPercent, // Use safe variable
                            PageCount = pageCount,                     // Use safe variable
                            MaintenanceAction = action,
                            MaintenanceScript = script
                        });
                    }
                }
                return fragList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Index Fragmentation Access Error: {ex.Message}", "Maintenance Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<IndexFragmentation>();
            }
        }

        public List<BlockingProcess> GetCurrentBlockingChain()
        {
            try
            {
                // T-SQL to find sessions currently being blocked
                string sql = @"
                    SELECT 
                        r.session_id AS SessionId,
                        r.blocking_session_id AS BlockingSessionId,
                        r.wait_time AS WaitTimeMS,
                        r.wait_type AS WaitType,
                        t.text AS BlockedCommand
                    FROM sys.dm_exec_requests r
                    CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
                    WHERE r.blocking_session_id <> 0 -- Find only blocked sessions
                    ORDER BY r.wait_time DESC;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var blockingList = new List<BlockingProcess>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        // Safely retrieve the raw BlockedCommand object (can be DBNull)
                        object blockedCommandObj = row["BlockedCommand"];

                        // Convert to string, or default to "" if DBNull/Null is found.
                        string fullBlockedCommand = (blockedCommandObj is DBNull)
                                                    ? string.Empty
                                                    : blockedCommandObj.ToString() ?? string.Empty;

                        // Trim the whitespace from the resulting string.
                        string trimmedCommand = fullBlockedCommand.Trim();

                        blockingList.Add(new BlockingProcess
                        {
                            SessionId = Convert.ToInt16(row["SessionId"]),
                            BlockingSessionId = Convert.ToInt16(row["BlockingSessionId"]),
                            WaitTimeMS = Convert.ToInt32(row["WaitTimeMS"]),

                            // WaitType is often N/A or a short code, use null-coalescing
                            WaitType = row["WaitType"]?.ToString() ?? "N/A",

                            // Assign the safely retrieved and trimmed string
                            BlockedCommand = trimmedCommand
                        });
                    }
                }
                return blockingList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blocking Chain Access Error: {ex.Message}", "Troubleshooting Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<BlockingProcess>();
            }
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
            try
            {
                string sql = @"
            SELECT
                p.name AS PrincipalName,
                p.type_desc AS PrincipalType,
                STUFF((
                    SELECT ', ' + dp.name
                    FROM sys.database_principals dp
                    JOIN sys.database_role_members rm ON rm.role_principal_id = dp.principal_id
                    WHERE rm.member_principal_id = p.principal_id
                    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS RoleMembership
            FROM sys.database_principals p
            WHERE p.sid IS NOT NULL AND p.name NOT LIKE '##%';";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var principals = new List<DatabasePrincipal>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        principals.Add(new DatabasePrincipal
                        {
                            Name = row["PrincipalName"]?.ToString() ?? "N/A",
                            Type = row["PrincipalType"]?.ToString() ?? "N/A",
                            RoleMembership = row["RoleMembership"]?.ToString() ?? "public"
                        });
                    }
                }
                return principals;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Principal Access Error: {ex.Message}", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<DatabasePrincipal>();
            }
        }

        public List<SecurityEvent> GetRecentSecurityEvents()
        {
            try
            {
                string sql = @"
                    DECLARE @TraceFileName NVARCHAR(1000);

                    -- 1. Get the path to the active default trace file using CONVERT
                    SELECT TOP 1 @TraceFileName = CONVERT(NVARCHAR(1000), value) 
                    FROM sys.fn_trace_getinfo(default) 
                    WHERE property = 2
                    ORDER BY traceid DESC;

                    -- 2. Query the trace file using the full path
                    SELECT TOP 50
                        StartTime,
                        HostName AS ClientHost,
                        ApplicationName AS ApplicationName,
                        LoginName,
                        CASE WHEN EventClass = 20 THEN 'Login Failed' 
                             WHEN EventClass = 17 THEN 'Login Succeeded'
                             ELSE 'Other Audit Event' 
                        END AS EventType,
                        CASE WHEN Success = 1 THEN 'True' ELSE 'False' END AS Success,
                        DatabaseName -- Include this for debugging context
                    FROM sys.fn_trace_gettable(@TraceFileName, default) AS trace
                    WHERE EventClass IN (20, 17) -- 20: Login Failed, 17: Login Succeeded
                    ORDER BY StartTime DESC;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var securityEvents = new List<SecurityEvent>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        securityEvents.Add(new SecurityEvent
                        {
                            EventTime = (row["StartTime"] is DBNull) ? DateTime.MinValue : Convert.ToDateTime(row["StartTime"]),
                            LoginName = row["LoginName"]?.ToString() ?? "N/A",
                            ClientHost = row["ClientHost"]?.ToString() ?? "N/A",
                            EventType = row["EventType"]?.ToString() ?? "N/A",
                            Success = row["Success"]?.ToString() ?? "N/A"
                        });
                    }
                }
                return securityEvents;
            }
            catch (Exception ex)
            {
                // CRITICAL FIX: Use single double quotes and ensure the string is valid.
                MessageBox.Show($"Security Events Access Error: You may lack VIEW SERVER STATE permission or the trace file is inaccessible. Details: {ex.Message}",
                                "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<SecurityEvent>();
            }
        }

        // Services/MetadataService.cs (Modified GetMaskingCandidates method)

        public List<MaskingCandidate> GetMaskingCandidates()
        {
            try
            {
                // FINAL ROBUST T-SQL FIX: We must use Dynamic SQL (SET @sql = N'...') 
                // to bypass the static parser error that occurs even in SQL Server 2022.
                string dynamicSql = @"
                    DECLARE @sql NVARCHAR(MAX);
            
                    SELECT
                        QUOTENAME(s.name) + '.' + QUOTENAME(t.name) AS SchemaTableName,
                        c.name AS ColumnName,
                        ty.name AS DataType,
                        mc.masking_function AS MaskingFunction,
                        CASE WHEN mc.masking_function IS NOT NULL THEN 1 ELSE 0 END AS IsMasked
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    INNER JOIN sys.types ty ON c.system_type_id = ty.system_type_id
                    LEFT JOIN sys.masked_columns mc ON c.object_id = mc.object_id AND c.column_id = mc.column_id
                    WHERE 
                        mc.masking_function IS NOT NULL -- Already masked (found via the LEFT JOIN)
                        OR (
                            c.name  LIKE '%email%' 
                            OR c.name LIKE '%ssn%' 
                            OR c.name LIKE '%sec_no%' 
                            OR c.name LIKE '%passw%'
                           )
                        OR ty.name IN ('char', 'varchar', 'nvarchar', 'varbinary','text', 'ntext')
                    ORDER BY SchemaTableName, ColumnName;
                ";

                // Note: SqlConnectionManager.ExecuteQuery handles the result set from the EXEC call.
                DataTable dt = SqlConnectionManager.ExecuteQuery(dynamicSql);
                var candidates = new List<MaskingCandidate>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        // CRITICAL FIX: Read IsMasked safely from the generated column
                        bool isMasked = row["IsMasked"] is not DBNull && Convert.ToBoolean(row["IsMasked"]);

                        string tableName = row["SchemaTableName"]?.ToString() ?? "N/A";
                        string columnName = row["ColumnName"]?.ToString() ?? "N/A";
                        string dataType = row["DataType"]?.ToString() ?? "N/A";
                        string maskingFunction = row["MaskingFunction"]?.ToString() ?? "partial(1,'xxxx',0)";

                        // Construct the DDL script
                        string script = isMasked
                            ? $"ALTER TABLE {tableName} ALTER COLUMN {columnName} {dataType} NO MASKED;" // Script to UNMASK
                            : $"ALTER TABLE {tableName} ALTER COLUMN {columnName} ADD MASKED WITH (FUNCTION = '{maskingFunction}');"; // Script to MASK

                        candidates.Add(new MaskingCandidate
                        {
                            SchemaTableName = tableName,
                            ColumnName = columnName,
                            DataType = dataType,
                            IsMasked = isMasked,
                            MaskingFunction = maskingFunction,
                            MaskingScript = script
                        });
                    }
                }
                return candidates;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Data Masking Access Error (Dynamic SQL Failure): The system could not execute the query to check for masking candidates. Details: {ex.Message}", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<MaskingCandidate>();
            }
        }

        public List<ServerInfoDetail> GetServerInformation()
        {
            try
            {
                string sql = @"
                    SELECT 'SQL Server Version' AS PropertyName, @@VERSION AS Value
                    UNION ALL
                    SELECT 'Edition', SERVERPROPERTY('Edition')
                    UNION ALL
                    SELECT 'Instance Name', SERVERPROPERTY('InstanceName')
                    UNION ALL
                    SELECT 'Is Clustered', SERVERPROPERTY('IsClustered')
                    UNION ALL
                    SELECT 'Product Level', SERVERPROPERTY('ProductLevel')
                    UNION ALL
                    SELECT 'Database Compatibility Level', CAST(compatibility_level AS NVARCHAR(100))
                    FROM sys.databases WHERE name = DB_NAME()
                    UNION ALL
                    -- CRITICAL FIX: Use the confirmed working column: physical_memory_kb
                    SELECT 'Physical RAM (MB)', CAST((physical_memory_kb / 1024) AS NVARCHAR(100))
                    FROM sys.dm_os_sys_info
                    ORDER BY PropertyName;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var details = new List<ServerInfoDetail>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        details.Add(new ServerInfoDetail
                        {
                            PropertyName = row["PropertyName"]?.ToString() ?? "N/A",
                            Value = row["Value"]?.ToString() ?? "N/A"
                        });
                    }
                }
                return details;
            }
            catch (Exception ex)
            {
                // ... (existing catch block) ...
                MessageBox.Show($"Server Info Access Error: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<ServerInfoDetail>();
            }
        }

        public List<SchemaDifference> GetBaseSchema(string targetConnectionString)
        {
            // NOTE: This uses ADO.NET directly, bypassing the standard SqlConnectionManager
            // to handle the secondary connection string.

            string sql = @"
                SELECT 
                        t.name AS ObjectName,
                        c.name AS ColumnName,
                        ty.name AS DataType
                    FROM sys.tables t
                    LEFT JOIN sys.columns c ON t.object_id = c.object_id -- <<< CRITICAL LEFT JOIN FIX
                    LEFT JOIN sys.types ty ON c.system_type_id = ty.system_type_id
                    WHERE t.is_ms_shipped = 0 
                    ORDER BY t.name, c.column_id;";

            var schemaDetails = new List<SchemaDifference>();

            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(targetConnectionString))
                {
                    connection.Open();
                    using (var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection))
                    {
                        command.CommandTimeout = 300; // Use the long timeout
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                schemaDetails.Add(new SchemaDifference
                                {
                                    ObjectName = reader["ObjectName"]?.ToString() ?? string.Empty, // <<< Ensure safe access
                                    ColumnName = reader["ColumnName"]?.ToString() ?? string.Empty, // <<< Ensure safe access
                                    DataType = reader["DataType"]?.ToString() ?? string.Empty,     // <<< Ensure safe access
                                    DifferenceType = "Source"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // On failure, return only a single error object to indicate the problem.
                schemaDetails.Add(new SchemaDifference { DifferenceType = $"Error: {ex.Message}" });
            }
            return schemaDetails;
        }

        public List<SqlAgentJob> GetSqlAgentJobs()
        {
            try
            {
                // NOTE: This query targets msdb, which is accessible to all application databases.
                string sql = @"
            SELECT 
                j.name AS JobName,
                CASE WHEN j.enabled = 1 THEN 'Enabled' ELSE 'Disabled' END AS Status,
                c.name AS Category,
                s.next_run_date,
                s.next_run_time
            FROM msdb.dbo.sysjobs j
            INNER JOIN msdb.dbo.syscategories c ON j.category_id = c.category_id
            LEFT JOIN msdb.dbo.sysjobschedules s ON j.job_id = s.job_id
            WHERE c.name NOT LIKE 'REPL%' AND j.name NOT LIKE 'sys\_%'
            ORDER BY j.name;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var jobs = new List<SqlAgentJob>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        jobs.Add(new SqlAgentJob
                        {
                            JobName = row["JobName"]?.ToString() ?? "N/A",
                            Status = row["Status"]?.ToString() ?? "N/A",
                            Category = row["Category"]?.ToString() ?? "N/A",
                            // Convert integer date/time formats to strings for display
                            NextRunDate = row["next_run_date"].ToString() == "0" ? "N/A" : row["next_run_date"]?.ToString() ?? "N/A",
                            NextRunTime = row["next_run_time"].ToString() == "0" ? "N/A" : row["next_run_time"]?.ToString() ?? "N/A"
                        });
                    }
                }
                return jobs;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SQL Agent Job Access Error: {ex.Message}", "Administration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<SqlAgentJob>();
            }
        }

        public List<BackupHistory> GetBackupHistory()
        {
            try
            {
                // T-SQL to retrieve recent successful backups for the current database
                string sql = @"
            SELECT TOP 20
                s.database_name AS DatabaseName,
                s.backup_start_date AS BackupDate,
                CASE s.type 
                    WHEN 'D' THEN 'Full' 
                    WHEN 'I' THEN 'Differential' 
                    WHEN 'L' THEN 'Log' 
                    ELSE s.type END AS BackupType,
                s.user_name AS UserName,
                CAST((s.backup_size / 1024.0 / 1024.0) AS DECIMAL(10, 2)) AS SizeMB,
                m.physical_device_name AS DeviceName
            FROM msdb.dbo.backupset s
            INNER JOIN msdb.dbo.backupmediafamily m ON s.media_set_id = m.media_set_id
            WHERE s.database_name = DB_NAME() 
            ORDER BY s.backup_start_date DESC;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var history = new List<BackupHistory>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        history.Add(new BackupHistory
                        {
                            DatabaseName = row["DatabaseName"]?.ToString() ?? "N/A",
                            BackupDate = Convert.ToDateTime(row["BackupDate"]),
                            BackupType = row["BackupType"]?.ToString() ?? "N/A",
                            UserName = row["UserName"]?.ToString() ?? "N/A",
                            SizeMB = Convert.ToDecimal(row["SizeMB"]),
                            DeviceName = row["DeviceName"]?.ToString() ?? "N/A"
                        });
                    }
                }
                return history;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup History Access Error: {ex.Message}", "Maintenance Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<BackupHistory>();
            }
        }

        public List<ServerSetting> GetConfigurableSettings()
        {
            try
            {
                // T-SQL to pull all sp_configure settings and their current values
                string sql = @"
            SELECT 
                name AS Name, 
                value AS ConfigValue,
                value_in_use AS CurrentValue,
                description AS Description
            FROM sys.configurations
            ORDER BY name;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var settings = new List<ServerSetting>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string name = row["Name"]?.ToString() ?? "N/A";
                        string configValue = row["ConfigValue"]?.ToString() ?? "N/A";
                        string currentValue = row["CurrentValue"]?.ToString() ?? "N/A";

                        // Construct the DDL script template for actionability
                        string script = $"EXEC sp_configure '{name}', [NEW_VALUE]; RECONFIGURE;";

                        settings.Add(new ServerSetting
                        {
                            Name = name,
                            ConfigValue = configValue,
                            CurrentValue = currentValue,
                            Description = row["Description"]?.ToString() ?? "N/A",
                            ConfigurationScript = script
                        });
                    }
                }
                return settings;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server Configuration Access Error: {ex.Message}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<ServerSetting>();
            }
        }

        public List<ServerSetting> GetDatabaseConfiguration()
        {
            try
            {
                // T-SQL to retrieve critical database configuration settings for the CURRENT database.
                string sql = @"
                    SELECT 'Database Name' AS PropertyName, DB_NAME() AS Value
                    UNION ALL
                    SELECT 'Recovery Model', recovery_model_desc
                    FROM sys.databases WHERE name = DB_NAME()
                    UNION ALL
                    SELECT 'Compatibility Level', CAST(compatibility_level AS NVARCHAR(100))
                    FROM sys.databases WHERE name = DB_NAME()
                    UNION ALL
                    SELECT 'Auto Close', CAST(is_auto_close_on AS NVARCHAR(100))
                    FROM sys.databases WHERE name = DB_NAME()
                    UNION ALL
                    SELECT 'Auto Shrink', CAST(is_auto_shrink_on AS NVARCHAR(100))
                    FROM sys.databases WHERE name = DB_NAME()
                    ORDER BY PropertyName;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var details = new List<ServerSetting>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        details.Add(new ServerSetting
                        {
                            Name = row["PropertyName"]?.ToString() ?? "N/A",
                            CurrentValue = row["Value"]?.ToString() ?? "N/A"
                        });
                    }
                }
                return details;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Configuration Access Error: {ex.Message}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<ServerSetting>();
            }
        }

        public List<SqlSnippet> GetSnippetLibrary()
        {
            // These are examples; the list would be much longer in a final product.
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
                    Description = "Quickly identifies the head blocker and all sessions currently blocked by it. Run on the 'master' database.",
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
                    Description = "Retrieves the top 20 most CPU-intensive queries from the plan cache. Used for finding long-term resource hogs.",
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
                    Description = "Lists indexes with high fragmentation levels (over 30%) that should be rebuilt. Run against the target database.",
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
                    Description = "Reports on the space utilization (data and log) for the current database, showing total size and remaining free space.",
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
                    Description = "Queries the SQL Server Error Log to check for recent failed logins, including reason and source IP.",
                    Code = @"EXEC sys.xp_readerrorlog 0, 1, N'Login failed';"
                },
                new SqlSnippet
                {
                    Title = "Check Database Role Membership",
                    Category = "Security",
                    Description = "Shows all users and the database roles they belong to (e.g., db_datareader, db_writer). Run against the target database.",
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


                // ... (Add more snippets here) ...
            };
        }

        // CRITICAL FIX #3: GetQueryExecutionPlan() - Query Execution Restriction
        public string GetQueryExecutionPlan(string query)
        {
            // SECURITY: Validate and sanitize query input
            if (string.IsNullOrWhiteSpace(query))
            {
                return "ERROR: Query cannot be empty.";
            }

            // SECURITY: Only allow SELECT statements to prevent data modification
            string trimmedQuery = query.Trim();
            if (!trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return "ERROR: Only SELECT statements are allowed for execution plan analysis.";
            }

            // SECURITY: Restrict query length to prevent DoS
            if (query.Length > 10000)
            {
                return "ERROR: Query is too long (max 10000 characters).";
            }

            string? connectionString = SqlConnectionManager.GetCurrentConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection not established.");
            }

            string planXml = "Plan could not be retrieved.";

            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    connection.Open();

                    // SECURITY: Set command timeout to prevent hanging queries
                    int commandTimeout = 30; // 30 seconds max

                    using (var cmdPlanOn = new SqlCommand("SET SHOWPLAN_XML ON;", connection))
                    {
                        cmdPlanOn.CommandTimeout = commandTimeout;
                        cmdPlanOn.ExecuteNonQuery();
                    }

                    using (var cmdQuery = new SqlCommand(query, connection))
                    {
                        cmdQuery.CommandTimeout = commandTimeout;

                        using (var reader = cmdQuery.ExecuteXmlReader())
                        {
                            if (reader.Read())
                            {
                                planXml = reader.ReadOuterXml();
                            }
                        }
                    }

                    using (var cmdPlanOff = new SqlCommand("SET SHOWPLAN_XML OFF;", connection))
                    {
                        cmdPlanOff.CommandTimeout = commandTimeout;
                        cmdPlanOff.ExecuteNonQuery();
                    }
                }
                return planXml;
            }
            catch (SqlException ex)
            {
                // SECURITY: Don't expose detailed SQL errors to user
                System.Diagnostics.Debug.WriteLine($"SQL Error: {ex.Message}");
                return "ERROR retrieving plan. Please verify your query syntax.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return "ERROR: An unexpected error occurred.";
            }
        }

        public List<AvailabilityReplicaStatus> GetAvailabilityGroupStatus()
        {
            try
            {
                // T-SQL to query the primary AG DMVs (sys.dm_hadr_availability_replica_states).
                string sql = @"
                    SELECT
                        ar.replica_server_name AS InstanceName,
                        ars.role_desc AS Role,
                        ars.synchronization_health_desc AS SynchronizationHealth,
        
                        -- CRITICAL FIX: Use the widely available synchronization_health_desc column 
                        -- directly for the state description, and use a reliable CASE statement.
                        CASE 
                            WHEN ars.synchronization_health_desc = 'NOT_HEALTHY' THEN 'NOT_HEALTHY'
                            WHEN ars.synchronization_health_desc = 'HEALTHY' THEN 'SYNCHRONIZED'
                            ELSE 'SYNCHRONIZING' 
                        END AS SynchronizationState, -- Use this alias for binding in C#
        
                        drs.log_send_queue_size AS LogSendQueueKB,
                        drs.redo_queue_size AS RedoQueueKB
                    FROM sys.dm_hadr_availability_replica_states ars
                    INNER JOIN sys.availability_replicas ar ON ars.replica_id = ar.replica_id
                    INNER JOIN sys.dm_hadr_database_replica_states drs ON ars.replica_id = drs.replica_id
                    ORDER BY ars.role_desc DESC, ar.replica_server_name;";

                // Note: AG DMVs are accessible from any user database.
                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var statusList = new List<AvailabilityReplicaStatus>();

                if (dt is not null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        statusList.Add(new AvailabilityReplicaStatus
                        {
                            InstanceName = row["InstanceName"]?.ToString() ?? "N/A",
                            Role = row["Role"]?.ToString() ?? "N/A",
                            SynchronizationHealth = row["SynchronizationHealth"]?.ToString() ?? "N/A",
                            SynchronizationState = row["SynchronizationState"]?.ToString() ?? "N/A",
                            // Safely convert large integer values, defaulting to 0
                            LogSendQueueKB = (row["LogSendQueueKB"] is DBNull) ? 0 : Convert.ToInt64(row["LogSendQueueKB"]),
                            RedoQueueKB = (row["RedoQueueKB"] is DBNull) ? 0 : Convert.ToInt64(row["RedoQueueKB"])
                        });
                    }
                }
                return statusList;
            }
            catch (Exception ex)
            {
                // This will often fail if AGs are not configured on the instance.
                MessageBox.Show($"AG Status Error: {ex.Message}. Check if Always On is configured.", "HA Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<AvailabilityReplicaStatus>();
            }
        }

        public List<DriveSpaceInfo> GetDriveSpaceReport()
        {
            try
            {
                // 1. Get database file mapping (which database files reside on which drive)
                string fileMappingSql = @"
                    SELECT DISTINCT
                        LEFT(physical_name, 1) AS Drive,
                        name AS FileName,
                        database_id,
                        DB_NAME(database_id) AS DatabaseName
                    FROM sys.master_files
                    WHERE database_id > 4; -- Exclude system databases (master, model, msdb, tempdb)
                ";

                // 2. Get OS-level drive space using xp_fixeddrives
                string freeSpaceSql = @"
                    SELECT drive AS Drive, CAST([MB Free] AS BIGINT) AS MBFree 
                    FROM xp_fixeddrives();
                ";

                DataTable fileDt = SqlConnectionManager.ExecuteQuery(fileMappingSql);
                DataTable? spaceDt = null;

                try
                {
                    spaceDt = SqlConnectionManager.ExecuteQuery(freeSpaceSql);
                }
                catch (Exception ex)
                {
                    // If xp_fixeddrives fails (permissions), provide a fallback
                    MessageBox.Show($"Warning: Drive space query failed. Displaying database file locations with WMI capacity data. Details: {ex.Message}", 
                                    "Partial Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                var driveMap = new Dictionary<string, List<string>>();
                var spaceInfo = new List<DriveSpaceInfo>();

                // --- Step 1: Get WMI drive capacity information ---
                Dictionary<string, (decimal TotalGB, decimal FreeGB)> wmiDriveInfo = new();

                try
                {
                    wmiDriveInfo = DriveSpaceHelper.GetDriveSpaceFromWMI();
                }
                catch (Exception ex)
                {
                    // WMI failed - will use xp_fixeddrives data only
                    System.Diagnostics.Debug.WriteLine($"WMI query failed: {ex.Message}");
                }

                // --- Step 2: Map Database Files to Drives ---
                if (fileDt is not null && fileDt.Rows.Count > 0)
                {
                    foreach (DataRow row in fileDt.Rows)
                    {
                        string drive = row["Drive"]?.ToString()?.ToUpper() ?? "UNKNOWN";
                        string dbName = row["DatabaseName"]?.ToString() ?? "N/A";

                        if (!driveMap.ContainsKey(drive))
                        {
                            driveMap[drive] = new List<string>();
                        }
                        if (!driveMap[drive].Contains(dbName))
                        {
                            driveMap[drive].Add(dbName);
                        }
                    }
                }

                // --- Step 3: Process Free Space from xp_fixeddrives and merge with WMI data ---
                if (spaceDt is not null && spaceDt.Rows.Count > 0)
                {
                    foreach (DataRow row in spaceDt.Rows)
                    {
                        string drive = row["Drive"].ToString()?.ToUpper() ?? "N/A";
                        if (drive == "N/A") continue;

                        // Free space from xp_fixeddrives in MB
                        long freeSpaceMB = Convert.ToInt64(row["MBFree"]);
                        decimal freeSpaceGB = freeSpaceMB / 1024.0m; // Convert MB to GB

                        // Get WMI data if available
                        decimal totalCapacityGB = 0;
                        decimal percentFree = 0;

                        if (wmiDriveInfo.TryGetValue(drive + ":\\", out var wmiData))
                        {
                            totalCapacityGB = Math.Round(wmiData.TotalGB, 2);
                            // Calculate percent free using WMI total capacity
                            if (totalCapacityGB > 0)
                            {
                                percentFree = Math.Round((freeSpaceGB / totalCapacityGB) * 100, 2);
                            }
                        }

                        spaceInfo.Add(new DriveSpaceInfo
                        {
                            DriveLetter = drive,
                            TotalCapacityGB = totalCapacityGB,
                            FreeSpaceGB = Math.Round(freeSpaceGB, 2),
                            PercentFree = percentFree,
                            DatabaseFiles = driveMap.ContainsKey(drive)
                                ? string.Join(", ", driveMap[drive])
                                : "None"
                        });
                    }
                }
                else if (wmiDriveInfo.Count > 0)
                {
                    // Fallback: Use WMI data if xp_fixeddrives failed
                    foreach (var kvp in wmiDriveInfo)
                    {
                        string drive = kvp.Key.TrimEnd(':');

                        spaceInfo.Add(new DriveSpaceInfo
                        {
                            DriveLetter = drive,
                            TotalCapacityGB = Math.Round(kvp.Value.TotalGB, 2),
                            FreeSpaceGB = Math.Round(kvp.Value.FreeGB, 2),
                            PercentFree = kvp.Value.TotalGB > 0 
                                ? Math.Round((kvp.Value.FreeGB / kvp.Value.TotalGB) * 100, 2)
                                : 0,
                            DatabaseFiles = driveMap.ContainsKey(drive)
                                ? string.Join(", ", driveMap[drive])
                                : "None"
                        });
                    }
                }
                else if (driveMap.Count > 0)
                {
                    // Last fallback: If both xp_fixeddrives and WMI failed but we have database files
                    foreach (var kvp in driveMap)
                    {
                        spaceInfo.Add(new DriveSpaceInfo
                        {
                            DriveLetter = kvp.Key,
                            TotalCapacityGB = 0,
                            FreeSpaceGB = 0,
                            PercentFree = 0,
                            DatabaseFiles = string.Join(", ", kvp.Value)
                        });
                    }
                }

                // If still no data, return a message row
                if (spaceInfo.Count == 0)
                {
                    spaceInfo.Add(new DriveSpaceInfo
                    {
                        DriveLetter = "N/A",
                        TotalCapacityGB = 0,
                        FreeSpaceGB = 0,
                        PercentFree = 0,
                        DatabaseFiles = "No data available. Check xp_fixeddrives permissions or WMI access."
                    });
                }

                return spaceInfo;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Drive Space Access Error: {ex.Message}. Check xp_fixeddrives and WMI permissions.", "Capacity Planning Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<DriveSpaceInfo>
                {
                    new DriveSpaceInfo
                    {
                        DriveLetter = "ERROR",
                        TotalCapacityGB = 0,
                        FreeSpaceGB = 0,
                        PercentFree = 0,
                        DatabaseFiles = $"Error: {ex.Message}"
                    }
                };
            }
        }


    }
}