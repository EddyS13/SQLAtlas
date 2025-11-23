// Services/MetadataService.cs

using DatabaseVisualizer.Data;
using DatabaseVisualizer.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Windows; // Used for MessageBox.Show in try/catch blocks

namespace DatabaseVisualizer.Services
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
                MessageBox.Show($"Column Data Processing Error: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public ProcedureDetails GetObjectDetails(string schemaName, string objectName)
        {
            string qualifiedName = $"[{schemaName}].[{objectName}]";

            // 1. Get Definition and Dates
            string defSql = $@"
                SELECT 
                    m.definition AS Definition,
                    o.create_date AS CreateDate,
                    o.modify_date AS ModifyDate
                FROM sys.sql_modules m
                INNER JOIN sys.objects o ON m.object_id = o.object_id
                WHERE m.object_id = OBJECT_ID('{qualifiedName}')";

            DataTable defDt = SqlConnectionManager.ExecuteQuery(defSql);

            // Initialize with defaults
            string definition = "DEFINITION NOT FOUND";
            DateTime createDate = DateTime.MinValue;
            DateTime modifyDate = DateTime.MinValue;

            if (defDt is not null && defDt.Rows.Count > 0)
            {
                // Retrieve the raw object value for the definition
                object definitionValue = defDt.Rows[0]["Definition"];

                if (definitionValue is DBNull) // Check if the object is the specific DBNull sentinel
                {
                    // Value is null in the database, usually meaning the object is encrypted
                    definition = "DEFINITION IS ENCRYPTED";
                }
                else
                {
                    // Value is present; convert it to string using the null-coalescing operator
                    // to handle any unlikely reference null before assignment.
                    definition = definitionValue.ToString() ?? "DEFINITION NOT FOUND";
                }

                // Date parsing logic (remains mostly the same, using null checks for safety)
                object createDateValue = defDt.Rows[0]["CreateDate"];
                object modifyDateValue = defDt.Rows[0]["ModifyDate"];

                createDate = (createDateValue is not DBNull)
                             ? Convert.ToDateTime(createDateValue)
                             : DateTime.MinValue;

                modifyDate = (modifyDateValue is not DBNull)
                             ? Convert.ToDateTime(modifyDateValue)
                             : DateTime.MinValue;
            }

            // 2. Get Parameters
            string paramSql = $@"
                SELECT p.name AS Name, t.name AS DataType
                FROM sys.parameters p
                INNER JOIN sys.types t ON p.system_type_id = t.system_type_id
                WHERE p.object_id = OBJECT_ID('{qualifiedName}')
                ORDER BY p.parameter_id;";

            DataTable paramDt = SqlConnectionManager.ExecuteQuery(paramSql);
            var parameters = new List<ProcedureParameter>();

            if (paramDt != null)
            {
                foreach (DataRow row in paramDt.Rows)
                {
                    parameters.Add(new ProcedureParameter
                    {
                        Name = row["Name"]?.ToString() ?? string.Empty,
                        DataType = row["DataType"]?.ToString() ?? string.Empty,
                    });
                }
            }

            return new ProcedureDetails
            {
                Definition = definition,
                Parameters = parameters,
                CreateDate = createDate,
                ModifyDate = modifyDate
            };
        }

        public List<Dependency> GetObjectDependencies(string schemaName, string objectName)
        {
            try
            {
                string qualifiedName = $"[{schemaName}].[{objectName}]";

                // 1. Safely retrieve the Object ID (The query must return only the ID)
                string objectIdSql = $"SELECT OBJECT_ID('{qualifiedName}')";
                object? objectIdResult = SqlConnectionManager.ExecuteScalar(objectIdSql);

                // 2. Check if the ID is valid and not null
                if (objectIdResult is null || objectIdResult is DBNull)
                {
                    // If the object is not found, we cannot run the dependency query.
                    return new List<Dependency>();
                }

                // 3. Convert the object result to the correct integer type for filtering
                int objectId = Convert.ToInt32(objectIdResult);

                // Use the object_id of the procedure/function as the referencing entity
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
                    WHERE dep.referencing_id = OBJECT_ID(@QualifiedName);"; // Using object ID based on qualified name

                var parameters = new Dictionary<string, object> { { "@QualifiedName", qualifiedName } };
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
                MessageBox.Show($"Dependency Lookup Error: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
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




    }
}