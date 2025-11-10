// Services/MetadataService.cs

using DatabaseVisualizer.Data;
using DatabaseVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Data;
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
                        Name = row["name"].ToString().Trim(),
                        Type = row["type"].ToString().Trim().ToUpperInvariant(),
                        TypeDescription = row["type_desc"].ToString().Trim(),
                        SchemaName = row["SchemaName"].ToString().Trim()
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
                            ConstraintName = row["ConstraintName"].ToString(),
                            ParentTable = row["ParentTable"].ToString(),
                            ParentColumn = row["ParentColumn"].ToString(),
                            ReferencedTable = row["ReferencedTable"].ToString(),
                            ReferencedColumn = row["ReferencedColumn"].ToString(),
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
                            RelationshipType = row["RelationshipType"].ToString(),
                            ConnectedTable = row["ConnectedTable"].ToString()
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

            if (defDt != null && defDt.Rows.Count > 0)
            {
                // Check for DBNull and ENCRYPTED flag
                definition = (defDt.Rows[0]["Definition"] != DBNull.Value) ? defDt.Rows[0]["Definition"].ToString() : "DEFINITION IS ENCRYPTED";
                createDate = (defDt.Rows[0]["CreateDate"] != DBNull.Value) ? Convert.ToDateTime(defDt.Rows[0]["CreateDate"]) : DateTime.MinValue;
                modifyDate = (defDt.Rows[0]["ModifyDate"] != DBNull.Value) ? Convert.ToDateTime(defDt.Rows[0]["ModifyDate"]) : DateTime.MinValue;
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
                        Name = row["Name"].ToString(),
                        DataType = row["DataType"].ToString()
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

                // Use the object_id of the procedure/function as the referencing entity
                string sql = $@"
                    SELECT
                        CASE 
                            WHEN dep.referenced_class = 1 THEN 'Touches Table/View' 
                            WHEN dep.referenced_class = 6 THEN 'Calls Stored Proc/Func'
                            ELSE 'Other'
                        END AS Type,
                        QUOTENAME(s_ref.name) + '.' + QUOTENAME(o_ref.name) AS ObjectName
                    FROM sys.sql_expression_dependencies dep
                    INNER JOIN sys.objects o_ref ON dep.referenced_id = o_ref.object_id
                    INNER JOIN sys.schemas s_ref ON o_ref.schema_id = s_ref.schema_id
                    WHERE dep.referencing_id = OBJECT_ID(@QualifiedName);";

                var parameters = new Dictionary<string, object> { { "@QualifiedName", qualifiedName } };
                DataTable dt = SqlConnectionManager.ExecuteQuery(sql, parameters);
                var dependencies = new List<Dependency>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        dependencies.Add(new Dependency
                        {
                            Type = row["Type"].ToString(),
                            ObjectName = row["ObjectName"].ToString()
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
                        Status = (row["Status"] != DBNull.Value) ? row["Status"].ToString() : "SLEEPING",
                        Command = (row["Command"] != DBNull.Value) ? row["Command"].ToString() : "NONE",

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

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        queries.Add(new LongRunningQuery
                        {
                            SessionId = Convert.ToInt16(row["SessionId"]),
                            Status = row["Status"]?.ToString() ?? "N/A",
                            CommandDurationSeconds = Convert.ToInt32(row["DurationSeconds"]),
                            // Get the SQL statement, trimming excessive length if necessary
                            SqlStatement = row["SqlStatement"]?.ToString().Substring(0, Math.Min(row["SqlStatement"].ToString().Length, 500)) ?? "N/A"
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

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        metrics.Add(new CachedQueryMetric
                        {
                            ExecutionCount = Convert.ToInt64(row["ExecutionCount"]),
                            TotalCPUTimeMS = (Convert.ToInt64(row["TotalCPUTime"]) / 1000).ToString("N0"),
                            TotalLogicalReads = Convert.ToInt64(row["TotalLogicalReads"]).ToString("N0"),
                            AvgCPUTimeMS = (Convert.ToInt64(row["AvgCPUTime"]) / 1000).ToString("N0"),
                            QueryStatement = row["QueryStatement"]?.ToString().Trim() ?? "N/A"
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
                // T-SQL to find fragmented indexes (> 5% fragmentation and > 1000 pages)
                string sql = @"
                SELECT 
                    QUOTENAME(s.name) + '.' + QUOTENAME(t.name) AS TableName,
                    i.name AS IndexName,
                    ps.avg_fragmentation_in_percent AS FragmentationPercent,
                    ps.page_count AS PageCount,
                    CASE 
                        WHEN ps.avg_fragmentation_in_percent > 30 THEN 'REBUILD (CRITICAL)'
                        WHEN ps.avg_fragmentation_in_percent > 5 THEN 'REORGANIZE'
                        ELSE 'OK'
                    END AS MaintenanceAction
                FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps
                INNER JOIN sys.tables t ON ps.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
                WHERE ps.avg_fragmentation_in_percent > 5 AND ps.page_count > 1000
                ORDER BY ps.avg_fragmentation_in_percent DESC;";

                DataTable dt = SqlConnectionManager.ExecuteQuery(sql);
                var fragList = new List<IndexFragmentation>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        double fragmentationPercent = (row["FragmentationPercent"] != DBNull.Value) ? Convert.ToDouble(row["FragmentationPercent"]) : 0.0;

                        string action;
                        string script;

                        if (fragmentationPercent > 30)
                        {
                            action = "REBUILD (CRITICAL)";
                            // NOTE: Use the full qualified name for DDL safety
                            string fullTableName = row["TableName"]?.ToString() ?? "N/A";
                            string indexName = row["IndexName"]?.ToString() ?? "N/A";
                            script = $"ALTER INDEX {indexName} ON {fullTableName} REBUILD WITH (ONLINE = ON);";
                        }
                        else if (fragmentationPercent > 5)
                        {
                            action = "REORGANIZE";
                            string fullTableName = row["TableName"]?.ToString() ?? "N/A";
                            string indexName = row["IndexName"]?.ToString() ?? "N/A";
                            script = $"ALTER INDEX {indexName} ON {fullTableName} REORGANIZE;";
                        }
                        else
                        {
                            action = "OK (No Action)";
                            script = "N/A";
                        }

                        fragList.Add(new IndexFragmentation
                        {
                            TableName = row["TableName"]?.ToString() ?? "N/A",
                            IndexName = row["IndexName"]?.ToString() ?? "N/A",
                            FragmentationPercent = Convert.ToDouble(row["FragmentationPercent"]),
                            PageCount = Convert.ToInt64(row["PageCount"]),
                            MaintenanceAction = action, // <<< This populates the button text
                            MaintenanceScript = script  // <<< This is the executable command   
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

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        blockingList.Add(new BlockingProcess
                        {
                            SessionId = Convert.ToInt16(row["SessionId"]),
                            BlockingSessionId = Convert.ToInt16(row["BlockingSessionId"]),
                            WaitTimeMS = Convert.ToInt32(row["WaitTimeMS"]),
                            WaitType = row["WaitType"]?.ToString() ?? "N/A",
                            BlockedCommand = row["BlockedCommand"]?.ToString().Trim() ?? "N/A"
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



    }
}