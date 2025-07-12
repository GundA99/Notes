using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBMigrator.DBMigrator.Dto;
namespace DBMigratorBLogic.Migrator

{
    public class MigratorBLogic : IMigratorBLogic
    {

        #region For Swagger
        public void MigrateDataOnly(List<string>? schemaList)
        {
            //var schemaList =new List<string> {"Base","Portfolio"};
            string SqlConnectionString = "Server=localhost; Database=i1CoreDB; User=sa; Password=Ikione@123; Trusted_Connection=False; TrustServerCertificate=True;";
            var postgryConnectionString = "Server=localhost;Port=5432;Database=i1CoreDB;UserId=postgres;Password=Ikione@123";
            var postgryConnection = new NpgsqlConnection(postgryConnectionString);
            var SqlConnection = new SqlConnection(SqlConnectionString);

            postgryConnection.Open();
            SqlConnection.Open();


            var Sqlschema = SqlConnection.Query<string>(
                                      @"SELECT schema_name 
                                      FROM information_schema.schemata
                                      WHERE schema_name NOT IN ('sys', 'information_schema')"
                               ).ToList();

            var PostGryschema = postgryConnection.Query<string>(
                                   @"SELECT schema_name 
                                   FROM information_schema.schemata"
                               ).ToList();

            var allData = new List<Dictionary<string, object>>();

            foreach (var schema in Sqlschema)
            {
                if (schemaList.Contains(schema) || schemaList.Count == 0)
                {
                    if (PostGryschema.Contains(schema))
                    {
                        var Sqltables = SqlConnection.Query<string>(
                            $"SELECT table_name " +
                            $" FROM information_schema.tables " +
                            $" WHERE table_schema = '{schema}' AND table_type = 'BASE TABLE'"
                        ).ToList();

                        //     if (schema != "Base")
                        {
                            foreach (var table in Sqltables)
                            {
                                // 1. Get PK column(s) for this table
                                var pkColumns = SqlConnection.Query<string>(
                                    $@"
                                SELECT kcu.COLUMN_NAME
                                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                                ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                                AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                                AND tc.TABLE_SCHEMA = @schema
                                AND tc.TABLE_NAME = @table",
                                    new { schema, table }).Select(c => c).ToHashSet();
                                var query = $"SELECT * FROM {schema}.[{table}]";
                                var dataRows = SqlConnection.Query(query);

                                //if (table.ToLower() == "permissions")
                                foreach (IDictionary<string, object> row in dataRows)
                                {
                                    var isInsertWithoutPKsPossible = row.Keys.All(k => !pkColumns.Contains(k));

                                    List<string> columns;
                                    if (isInsertWithoutPKsPossible)
                                    {
                                        columns = row.Keys
                                            .Where(k => !pkColumns.Contains(k))
                                            .ToList();
                                    }
                                    else
                                    {
                                        columns = row.Keys.ToList(); // Use all columns
                                    }

                                    var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
                                    var paramList = string.Join(", ", columns.Select(c => $"@{c}"));

                                    var insertSql = $"INSERT INTO \"{schema}\".\"{table}\" ({columnList}) VALUES ({paramList})";

                                    var parameters = new DynamicParameters();
                                    foreach (var kvp in row)
                                    {
                                        if (isInsertWithoutPKsPossible)
                                        {
                                            if (!pkColumns.Contains(kvp.Key))
                                                parameters.Add($"@{kvp.Key}", kvp.Value);
                                        }
                                        else
                                        {
                                            parameters.Add($"@{kvp.Key}", kvp.Value);
                                        }
                                    }

                                    postgryConnection.Execute(insertSql, parameters);

                                }

                            }
                        }
                    }
                }
            }
        }
        public void MigrateSchemaAndTables(List<string>? schemaList)
        {
            //var schemaList = new List<string> { "Base", "Portfolio" };

            string SqlConnectionString = "Server=localhost; Database=i1CoreDB; User=sa; Password=Ikione@123; Trusted_Connection=False; TrustServerCertificate=True;";
            var postgryConnectionString = "Server=localhost;Port=5432;Database=i1CoreDB;UserId=postgres;Password=Ikione@123";

            var postgryConnection = new NpgsqlConnection(postgryConnectionString);
            var SqlConnection = new SqlConnection(SqlConnectionString);

            postgryConnection.Open();
            SqlConnection.Open();

            var Sqlschema = SqlConnection.Query<string>(
                @"SELECT schema_name 
                FROM information_schema.schemata
                WHERE schema_name NOT IN ('sys', 'information_schema')"
            ).ToList();

            var PostGryschema = postgryConnection.Query<string>(
                @"SELECT schema_name 
                FROM information_schema.schemata"
            ).ToList();

            foreach (var schema in Sqlschema)
            {
                if (schemaList.Contains(schema) || schemaList.Count == 0)
                {
                    if (!PostGryschema.Contains(schema))
                    {
                        // Create schema in PostgreSQL if it doesn't exist
                        var createSchemaQuery = $"CREATE SCHEMA \"{schema}\"";
                        postgryConnection.Execute(createSchemaQuery);
                    }

                    var Sqltables = SqlConnection.Query<string>(
                        $"SELECT table_name " +
                        $" FROM information_schema.tables " +
                        $" WHERE table_schema = '{schema}' AND table_type = 'BASE TABLE'"
                    ).ToList();

                    foreach (var table in Sqltables)
                    {
                        var Columns = SqlConnection.Query($"SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}'");

                        var pkColumns = SqlConnection.Query<string>(
                            $@"
                            SELECT kcu.COLUMN_NAME
                            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                            ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                            AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                            AND tc.TABLE_SCHEMA = @schema
                            AND tc.TABLE_NAME = @table",
                            new { schema, table }).Select(c => c).ToHashSet();

                        StringBuilder createTableQuery = new StringBuilder($"CREATE TABLE \"{schema}\".\"{table}\" (");

                        var columnNames = new HashSet<string>();
                        foreach (var column in Columns)
                        {
                            var columnName = column.COLUMN_NAME;
                            if (!columnNames.Contains(columnName))
                            {
                                string columnDefinition = $"\"{column.COLUMN_NAME}\" {ConvertSqlServerTypeToPostgreSql(column.DATA_TYPE, column.CHARACTER_MAXIMUM_LENGTH)}";
                                createTableQuery.Append(columnDefinition + ", ");
                                columnNames.Add(columnName);
                            }
                        }

                        if (pkColumns.Count > 0)
                        {
                            createTableQuery.Append("PRIMARY KEY (");
                            foreach (var pkColumn in pkColumns)
                            {
                                createTableQuery.Append($"\"{pkColumn}\", ");
                            }
                            createTableQuery.Length -= 2; // Remove the last comma and space
                            createTableQuery.Append("), ");
                        }

                        createTableQuery.Length -= 2; // Remove the last comma and space
                        createTableQuery.Append(");");

                        using (var postgreSqlConnection = new NpgsqlConnection(postgryConnectionString))
                        {
                            postgreSqlConnection.Open();
                            postgreSqlConnection.Execute(createTableQuery.ToString());
                        }
                    }
                }
            }
        }
        public void MigrateForeignKeys(List<string>? schemaList)
        {
            string sqlConnectionString = "Server=localhost; Database=i1CoreDB; User=sa; Password=Ikione@123; Trusted_Connection=False; TrustServerCertificate=True;";
            string postgreConnectionString = "Server=localhost;Port=5432;Database=i1CoreDB;UserId=postgres;Password=Ikione@123";

            using var sqlConnection = new SqlConnection(sqlConnectionString);
            using var pgConnection = new NpgsqlConnection(postgreConnectionString);

            sqlConnection.Open();
            pgConnection.Open();

            var foreignKeys = sqlConnection.Query(@"
            SELECT
            fk.name AS ForeignKeyName,
            sch.name AS SchemaName,
            tp.name AS ParentTable,
            cp.name AS ParentColumn,
            tr.name AS ReferencedTable,
            cr.name AS ReferencedColumn
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
            INNER JOIN sys.schemas sch ON tp.schema_id = sch.schema_id
            INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
            INNER JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
            INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
            ORDER BY fk.name
            ");

            // Group foreign key parts (for multi-column FKs)
            var groupedFks = foreignKeys.GroupBy(fk => new
            {
                fk.ForeignKeyName,
                fk.SchemaName,
                fk.ParentTable,
                fk.ReferencedTable
            });

            foreach (var group in groupedFks)
            {
                if (schemaList.Contains(group.Key.SchemaName) || schemaList.Count == 0)
                {
                    var parentSchema = group.Key.SchemaName;
                    var parentTable = group.Key.ParentTable;
                    var referencedTable = group.Key.ReferencedTable;
                    var fkName = group.Key.ForeignKeyName;

                    var columns = group.Select(g => g.ParentColumn).ToList();
                    var refColumns = group.Select(g => g.ReferencedColumn).ToList();

                    var addConstraintQuery = $@"
                    ALTER TABLE ""{parentSchema}"".""{parentTable}""
                    ADD CONSTRAINT ""{fkName}""
                    FOREIGN KEY ({string.Join(", ", columns.Select(c => $"\"{c}\""))})
                    REFERENCES ""{parentSchema}"".""{referencedTable}"" ({string.Join(", ", refColumns.Select(c => $"\"{c}\""))});
";

                    try
                    {
                        pgConnection.Execute(addConstraintQuery);
                        Console.WriteLine($"Migrated FK: {fkName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to migrate FK {fkName}: {ex.Message}");
                    }
                }
            }
        }
        public void CopyStoredProcedures()
        {
            string SqlConnectionString = "Server=172.16.16.152; Database=i1CoreDB; User=sa; Password=Ikione@123/; Trusted_Connection=False; TrustServerCertificate=True;";
            var postgryConnectionString = "Server=localhost;Port=5432;Database=i1CoreDB;UserId=postgres;Password=Ikione@123";

            var pgConn = new NpgsqlConnection(postgryConnectionString);
            var sqlConn = new SqlConnection(SqlConnectionString);

            var procedures = sqlConn.Query(@"
            SELECT 
            SCHEMA_NAME(o.schema_id) AS SchemaName,
            o.name AS ProcedureName,
            sm.definition
            FROM sys.sql_modules sm
            INNER JOIN sys.objects o ON sm.object_id = o.object_id
            WHERE o.type = 'P'
            ");

            foreach (var proc in procedures)
            {
                string tsql = proc.definition;
                string pgsql = ConvertTsqlToPgsql(tsql);

                try
                {
                    pgConn.Execute(pgsql);
                    Console.WriteLine($"Migrated procedure: {proc.ProcedureName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to migrate {proc.ProcedureName}: {ex.Message}");
                }
            }
        }
        public void MigrateSchemaAndTablesFromPostgreToSqlServer(List<string>? schemaList)
        {
            string sqlConnectionString = "Server=localhost; Database=i1CoreDbTest; User=sa; Password=Ikione@123; Trusted_Connection=False; TrustServerCertificate=True;";
            string postgreConnectionString = "Server=localhost;Port=5432;Database=i1CoreDB;UserId=postgres;Password=Ikione@123";

            using var postgreConnection = new NpgsqlConnection(postgreConnectionString);
            using var sqlConnection = new SqlConnection(sqlConnectionString);

            postgreConnection.Open();
            sqlConnection.Open();

            var postgreSchemas = postgreConnection.Query<string>(
                @"SELECT schema_name 
                  FROM information_schema.schemata
                  WHERE schema_name NOT IN ('pg_catalog', 'information_schema')").ToList();

            var sqlSchemas = sqlConnection.Query<string>(
                @"SELECT schema_name 
                FROM information_schema.schemata").ToList();

            foreach (var schema in postgreSchemas)
            {
                if (schemaList.Count == 0 || schemaList.Contains(schema))
                {
                    string targetSchema = schema == "public" ? "dbo" : schema;

                    var schemaExists = sqlConnection.QueryFirstOrDefault<int>(
                        @"SELECT COUNT(*) FROM sys.schemas WHERE name = @schemaName", new { schemaName = targetSchema });

                    if (schemaExists == 0 && targetSchema != "dbo") // dbo should already exist
                    {
                        sqlConnection.Execute($"CREATE SCHEMA [{targetSchema}]");
                    }


                    var postgreTables = postgreConnection.Query<string>(
                        $@"SELECT table_name 
                   FROM information_schema.tables 
                   WHERE table_schema = @schema AND table_type = 'BASE TABLE'",
                        new { schema }).ToList();

                    foreach (var table in postgreTables)
                    {
                        var columns = postgreConnection.Query(
                            @"SELECT column_name, data_type, character_maximum_length 
                      FROM information_schema.columns 
                      WHERE table_schema = @schema AND table_name = @table",
                            new { schema, table });

                        var pkColumns = postgreConnection.Query<string>(
                            @"SELECT kcu.column_name
                      FROM information_schema.table_constraints tc
                      JOIN information_schema.key_column_usage kcu 
                        ON tc.constraint_name = kcu.constraint_name 
                       AND tc.table_schema = kcu.table_schema
                      WHERE tc.constraint_type = 'PRIMARY KEY'
                        AND tc.table_schema = @schema AND tc.table_name = @table",
                            new { schema, table }).ToHashSet();

                        var createTableSql = new StringBuilder($"CREATE TABLE [{schema}].[{table}] (");

                        foreach (var column in columns)
                        {
                            string columnName = column.column_name;
                            string dataType = ConvertPostgreTypeToSqlServer(column.data_type, column.character_maximum_length);
                            createTableSql.Append($"[{columnName}] {dataType}, ");
                        }

                        if (pkColumns.Count > 0)
                        {
                            createTableSql.Append("PRIMARY KEY (");
                            foreach (var pk in pkColumns)
                            {
                                createTableSql.Append($"[{pk}], ");
                            }
                            createTableSql.Length -= 2; // Remove trailing comma
                            createTableSql.Append("), ");
                        }

                        createTableSql.Length -= 2; // Remove last comma
                        createTableSql.Append(");");

                        sqlConnection.Execute(createTableSql.ToString());
                    }
                }
            }
        }
        public void MigrateDataOnlyFromPostgreToSqlServer(List<string>? schemaList)
        {
            string sqlConnectionString = "Server=localhost; Database=i1CoreDbTest; User=sa; Password=Ikione@123; Trusted_Connection=False; TrustServerCertificate=True;";
            string postgreConnectionString = "Server=localhost;Port=5432;Database=i1CoreDB;UserId=postgres;Password=Ikione@123";

            using var postgreConnection = new NpgsqlConnection(postgreConnectionString);
            using var sqlConnection = new SqlConnection(sqlConnectionString);

            postgreConnection.Open();
            sqlConnection.Open();

            var postgreSchemas = postgreConnection.Query<string>(
                @"SELECT schema_name 
                  FROM information_schema.schemata
                  WHERE schema_name NOT IN ('pg_catalog', 'information_schema')").ToList();

            var sqlSchemas = sqlConnection.Query<string>(
                @"SELECT schema_name 
                FROM information_schema.schemata").ToList();

            foreach (var schema in postgreSchemas)
            {
                if (schemaList == null || schemaList.Count == 0 || schemaList.Contains(schema))
                {
                    if (sqlSchemas.Contains(schema))
                    {
                        var postgreTables = postgreConnection.Query<string>(
                            @"SELECT table_name 
                            FROM information_schema.tables 
                            WHERE table_schema = @schema AND table_type = 'BASE TABLE'",
                            new { schema }).ToList();

                        foreach (var table in postgreTables)
                        {
                            var pkColumns = postgreConnection.Query<string>(
                                @"SELECT kcu.column_name
                                FROM information_schema.table_constraints tc
                                JOIN information_schema.key_column_usage kcu 
                                ON tc.constraint_name = kcu.constraint_name 
                                AND tc.table_schema = kcu.table_schema
                                WHERE tc.constraint_type = 'PRIMARY KEY'
                                AND tc.table_schema = @schema AND tc.table_name = @table",
                                new { schema, table }).ToHashSet();

                            var query = $"SELECT * FROM \"{schema}\".\"{table}\"";
                            var dataRows = postgreConnection.Query(query);

                            foreach (IDictionary<string, object> row in dataRows)
                            {
                                var isInsertWithoutPKsPossible = row.Keys.All(k => !pkColumns.Contains(k));

                                List<string> columns;
                                if (isInsertWithoutPKsPossible)
                                {
                                    columns = row.Keys
                                        .Where(k => !pkColumns.Contains(k))
                                        .ToList();
                                }
                                else
                                {
                                    bool isIdentityColumn = sqlConnection.QueryFirstOrDefault<int>(
                                    @"SELECT COUNT(*)
                                      FROM sys.identity_columns ic
                                      JOIN sys.objects o ON ic.object_id = o.object_id
                                      WHERE o.name = @table AND SCHEMA_NAME(o.schema_id) = @schema",
                                    new { table, schema }) > 0;

                                    columns = row.Keys
                                     .Where(k => !(isIdentityColumn && k.Equals("Id", StringComparison.OrdinalIgnoreCase)))
                                     .ToList();

                                }

                                var columnList = string.Join(", ", columns.Select(c => $"[{c}]"));
                                var paramList = string.Join(", ", columns.Select(c => $"@{c}"));
                                var insertSql = $"INSERT INTO [{schema}].[{table}] ({columnList}) VALUES ({paramList})";

                                var parameters = new DynamicParameters();
                                foreach (var column in columns)
                                {
                                    parameters.Add($"@{column}", row[column]);
                                }

                                try
                                {
                                    sqlConnection.Execute(insertSql, parameters);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Insert failed for [{schema}].[{table}]: {ex.Message}");
                                    // Optionally log or skip
                                }
                            }
                        }
                    }
                }
            }
        }       
        public void MigrateForeignKeysFromPostgreToSqlServer(List<string>? schemaList)
        {
            string sqlConnectionString = "Server=localhost; Database=i1CoreDbTest; User=sa; Password=Ikione@123; Trusted_Connection=False; TrustServerCertificate=True;";
            string postgreConnectionString = "Server=localhost;Port=5432;Database=i1CoreDB;UserId=postgres;Password=Ikione@123";

            using var sqlConnection = new SqlConnection(sqlConnectionString);
            using var pgConnection = new NpgsqlConnection(postgreConnectionString);

            sqlConnection.Open();
            pgConnection.Open();

            var foreignKeys = pgConnection.Query<ForeignKeyInfo>(@"
                SELECT
                    tc.constraint_name AS ForeignKeyName,
                    tc.table_schema AS SchemaName,
                    tc.table_name AS ParentTable,
                    kcu.column_name AS ParentColumn,
                    ccu.table_name AS ReferencedTable,
                    ccu.column_name AS ReferencedColumn
                FROM 
                    information_schema.table_constraints AS tc
                    JOIN information_schema.key_column_usage AS kcu
                      ON tc.constraint_name = kcu.constraint_name
                     AND tc.table_schema = kcu.table_schema
                    JOIN information_schema.constraint_column_usage AS ccu
                      ON ccu.constraint_name = tc.constraint_name
                     AND ccu.table_schema = tc.table_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                ORDER BY tc.constraint_name, kcu.ordinal_position
            ").ToList();

            var groupedFks = foreignKeys.GroupBy(fk => new
            {
                fk.ForeignKeyName,
                fk.SchemaName,
                fk.ParentTable,
                fk.ReferencedTable
            });

            foreach (var group in groupedFks)
            {
                var schema = group.Key.SchemaName;

                // Skip schemas not in the list (if list is provided)
                if (schemaList != null && schemaList.Count > 0 && !schemaList.Contains(schema))
                    continue;

                var parentTable = group.Key.ParentTable;
                var referencedTable = group.Key.ReferencedTable;
                var fkName = group.Key.ForeignKeyName;

                var columns = group.Select(g => g.ParentColumn).ToList();
                var refColumns = group.Select(g => g.ReferencedColumn).ToList();

                var addConstraintSql = $@"
                ALTER TABLE [{schema}].[{parentTable}]
                ADD CONSTRAINT [{fkName}]
                FOREIGN KEY ({string.Join(", ", columns.Select(c => $"[{c}]"))})
                REFERENCES [{schema}].[{referencedTable}] ({string.Join(", ", refColumns.Select(c => $"[{c}]"))});";

                try
                {
                    // Optional: Check if FK already exists in SQL Server (uncomment if needed)
                    var exists = sqlConnection.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = @Name",
                        new { Name = fkName });
                    if (exists > 0) continue;

                    sqlConnection.Execute(addConstraintSql);
                    Console.WriteLine($"✅ Migrated FK: {fkName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to migrate FK {fkName}: {ex.Message}");
                }
            }

            Console.WriteLine("✅ All foreign key constraints added successfully.");
        }
        public Dictionary<string, List<string>> Login(Authentication authentication)
        {
            var result = new Dictionary<string, List<string>>();
            string baseConnectionString = authentication.SqlServerName;

            try
            {
                using (var masterConnection = new SqlConnection(baseConnectionString))
                {
                    masterConnection.Open();

                    // Step 1: Get all user databases
                    var databases = masterConnection.Query<string>(
                        @"SELECT name 
                          FROM sys.databases 
                          WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')"
                    ).ToList();

                    // Step 2: Loop through each database and fetch schemas
                    foreach (var dbName in databases)
                    {
                        string dbConnectionString = $"{baseConnectionString};Initial Catalog={dbName}";

                        try
                        {
                            using (var dbConnection = new SqlConnection(dbConnectionString))
                            {
                                dbConnection.Open();

                                var schemas = dbConnection.Query<string>(
                                    @"SELECT schema_name 
                                      FROM information_schema.schemata
                                      WHERE schema_name NOT IN ('sys', 'information_schema')"
                                ).ToList();

                                result[dbName] = schemas;
                            }
                        }
                        catch (Exception ex)
                        {
                            result[dbName] = new List<string> { $"Error fetching schemas: {ex.Message}" };
                        }
                    }
                }

                return result;
            }
            catch (SqlException ex)
            {
                throw new Exception("SQL Connection failed: " + ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred: " + ex.Message);
            }
        }
        #endregion

        public void MigrateSchemaAndTablesUI(string sqlBaseConnectionString, string pgBaseConnectionString, string SqldbName, List<string>? schemaList)
        {         
            var SqlConnectionString = $"{sqlBaseConnectionString};Database={SqldbName};";
            var postgryConnectionString = $"{pgBaseConnectionString};Database={SqldbName};";

            var postgryConnection = new NpgsqlConnection(postgryConnectionString);
            var SqlConnection = new SqlConnection(SqlConnectionString);

            postgryConnection.Open();
            SqlConnection.Open();

            var Sqlschema = SqlConnection.Query<string>(
                @"SELECT schema_name 
                FROM information_schema.schemata
                WHERE schema_name NOT IN ('sys', 'information_schema')"
            ).ToList();

            var PostGryschema = postgryConnection.Query<string>(
                @"SELECT schema_name 
                FROM information_schema.schemata"
            ).ToList();

            foreach (var schema in Sqlschema)
            {
                if (schemaList.Contains(schema) || schemaList.Count == 0)
                {
                    if (!PostGryschema.Contains(schema))
                    {
                        // Create schema in PostgreSQL if it doesn't exist
                        var createSchemaQuery = $"CREATE SCHEMA \"{schema}\"";
                        postgryConnection.Execute(createSchemaQuery);
                    }

                    var Sqltables = SqlConnection.Query<string>(
                        $"SELECT table_name " +
                        $" FROM information_schema.tables " +
                        $" WHERE table_schema = '{schema}' AND table_type = 'BASE TABLE'"
                    ).ToList();

                    foreach (var table in Sqltables)
                    {
                        var Columns = SqlConnection.Query($"SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}'");

                        var pkColumns = SqlConnection.Query<string>(
                            $@"
                            SELECT kcu.COLUMN_NAME
                            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                            ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                            AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                            AND tc.TABLE_SCHEMA = @schema
                            AND tc.TABLE_NAME = @table",
                            new { schema, table }).Select(c => c).ToHashSet();

                        StringBuilder createTableQuery = new StringBuilder($"CREATE TABLE \"{schema}\".\"{table}\" (");

                        var columnNames = new HashSet<string>();
                        foreach (var column in Columns)
                        {
                            var columnName = column.COLUMN_NAME;
                            if (!columnNames.Contains(columnName))
                            {
                                string columnDefinition = $"\"{column.COLUMN_NAME}\" {ConvertSqlServerTypeToPostgreSql(column.DATA_TYPE, column.CHARACTER_MAXIMUM_LENGTH)}";
                                createTableQuery.Append(columnDefinition + ", ");
                                columnNames.Add(columnName);
                            }
                        }

                        if (pkColumns.Count > 0)
                        {
                            createTableQuery.Append("PRIMARY KEY (");
                            foreach (var pkColumn in pkColumns)
                            {
                                createTableQuery.Append($"\"{pkColumn}\", ");
                            }
                            createTableQuery.Length -= 2; // Remove the last comma and space
                            createTableQuery.Append("), ");
                        }

                        createTableQuery.Length -= 2; // Remove the last comma and space
                        createTableQuery.Append(");");

                        using (var postgreSqlConnection = new NpgsqlConnection(postgryConnectionString))
                        {
                            postgreSqlConnection.Open();
                            postgreSqlConnection.Execute(createTableQuery.ToString());
                        }
                    }
                }
            }
        }

        public void MigrateDataOnlyUI(string sqlBaseConnectionString, string pgBaseConnectionString, string SqldbName, List<string>? schemaList)
        {
            //var schemaList =new List<string> {"Base","Portfolio"};
            var SqlConnectionString = $"{sqlBaseConnectionString};Database={SqldbName};";
            var postgryConnectionString = $"{pgBaseConnectionString};Database={SqldbName};";

            var postgryConnection = new NpgsqlConnection(postgryConnectionString);
            var SqlConnection = new SqlConnection(SqlConnectionString);

            postgryConnection.Open();
            SqlConnection.Open();


            var Sqlschema = SqlConnection.Query<string>(
                                      @"SELECT schema_name 
                                      FROM information_schema.schemata
                                      WHERE schema_name NOT IN ('sys', 'information_schema')"
                               ).ToList();

            var PostGryschema = postgryConnection.Query<string>(
                                   @"SELECT schema_name 
                                   FROM information_schema.schemata"
                               ).ToList();

            var allData = new List<Dictionary<string, object>>();

            foreach (var schema in Sqlschema)
            {
                if (schemaList.Contains(schema) || schemaList.Count == 0)
                {
                    if (PostGryschema.Contains(schema))
                    {
                        var Sqltables = SqlConnection.Query<string>(
                            $"SELECT table_name " +
                            $" FROM information_schema.tables " +
                            $" WHERE table_schema = '{schema}' AND table_type = 'BASE TABLE'"
                        ).ToList();

                        //     if (schema != "Base")
                        {
                            foreach (var table in Sqltables)
                            {
                                // 1. Get PK column(s) for this table
                                var pkColumns = SqlConnection.Query<string>(
                                $@"
                                SELECT kcu.COLUMN_NAME
                                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                                ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                                AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                                AND tc.TABLE_SCHEMA = @schema
                                AND tc.TABLE_NAME = @table",
                                    new { schema, table }).Select(c => c).ToHashSet();
                                var query = $"SELECT * FROM {schema}.[{table}]";
                                var dataRows = SqlConnection.Query(query);

                                //if (table.ToLower() == "permissions")
                                foreach (IDictionary<string, object> row in dataRows)
                                {
                                    var isInsertWithoutPKsPossible = row.Keys.All(k => !pkColumns.Contains(k));

                                    List<string> columns;
                                    if (isInsertWithoutPKsPossible)
                                    {
                                        columns = row.Keys
                                            .Where(k => !pkColumns.Contains(k))
                                            .ToList();
                                    }
                                    else
                                    {
                                        columns = row.Keys.ToList(); // Use all columns
                                    }

                                    var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
                                    var paramList = string.Join(", ", columns.Select(c => $"@{c}"));

                                    var insertSql = $"INSERT INTO \"{schema}\".\"{table}\" ({columnList}) VALUES ({paramList})";

                                    var parameters = new DynamicParameters();
                                    foreach (var kvp in row)
                                    {
                                        if (isInsertWithoutPKsPossible)
                                        {
                                            if (!pkColumns.Contains(kvp.Key))
                                                parameters.Add($"@{kvp.Key}", kvp.Value);
                                        }
                                        else
                                        {
                                            parameters.Add($"@{kvp.Key}", kvp.Value);
                                        }
                                    }

                                    postgryConnection.Execute(insertSql, parameters);

                                }

                            }
                        }
                    }
                }
            }
        }
        #region Private methods
        private string ConvertSqlServerTypeToPostgreSql(string sqlServerType, int? maxLength)
        {
            switch (sqlServerType.ToLower())
            {
                case "int":
                    return "INTEGER";
                case "bigint":
                    return "BIGINT";
                case "bit":
                    return "BOOLEAN";
                case "tinyint":
                    return "SMALLINT";
                case "smallint":
                    return "SMALLINT";
                case "uniqueidentifier":
                    return "UUID";
                case "varbinary":
                    return "BYTEA";
                case "float":
                    return "DOUBLE PRECISION";
                case "char":
                case "nchar":
                case "varchar":
                case "nvarchar":
                    return maxLength.HasValue && maxLength.Value > 0 ? $"VARCHAR({maxLength.Value})" : "TEXT";
                case "datetime":
                case "smalldatetime":
                case "datetime2":
                    return "TIMESTAMP";
                case "decimal":
                case "numeric":
                    return "NUMERIC";
                case "date":
                    return "DATE"; // Add support for 'date' type
                default:
                    throw new NotSupportedException($"SQL Server type '{sqlServerType}' is not supported.");
            }
        }
        public string ConvertTsqlToPgsql(string tsql)
        {
            // More robust handling needed
            return tsql
         .Replace("GETDATE()", "CURRENT_TIMESTAMP")
         .Replace("PRINT", "RAISE NOTICE")
         .Replace("NVARCHAR", "VARCHAR")
         .Replace("DATETIME", "TIMESTAMP")
         .Replace("BIT", "BOOLEAN")
         .Replace("ISNULL", "COALESCE")
         .Replace("BEGIN", "BEGIN")
         .Replace("END", "END")
         .Replace("IF", "IF")
         .Replace("ELSE", "ELSE")
         .Replace("SELECT", "SELECT")
         .Replace("FROM", "FROM")
         .Replace("WHERE", "WHERE")
         .Replace(">", ">")
         .Replace(";", ";")
         .Replace("@", "p_")
         .Replace("CREATE PROC", "CREATE OR REPLACE PROCEDURE")
         .Replace("AS", "AS $$")
         .Replace("BEGIN", "BEGIN")
         .Replace("END", "END $$ LANGUAGE plpgsql;");
        }
        private string ConvertPostgreTypeToSqlServer(string pgType, int? length, bool isPrimaryKey = false)
        {
            switch (pgType.ToLower())
            {
                case "character varying":
                case "varchar":
                    if (isPrimaryKey)
                        return length.HasValue && length > 0 ? $"VARCHAR({length})" : "VARCHAR(255)";
                    return length.HasValue ? $"VARCHAR({length})" : "VARCHAR(MAX)";
                case "character":
                case "char":
                    return length.HasValue ? $"CHAR({length})" : "CHAR(1)";
                case "text":
                    return isPrimaryKey ? "VARCHAR(255)" : "VARCHAR(MAX)";
                case "integer":
                case "int":
                    return "INT";
                case "bigint":
                    return "BIGINT";
                case "smallint":
                    return "SMALLINT";
                case "uuid":
                    return "UNIQUEIDENTIFIER";
                case "boolean":
                    return "BIT";
                case "date":
                    return "DATE";
                case "timestamp":
                case "timestamp without time zone":
                case "timestamp with time zone":
                    return "DATETIME";
                case "numeric":
                case "decimal":
                    return "DECIMAL(18,2)";
                case "double precision":
                    return "FLOAT";
                case "real":
                    return "REAL";
                case "bytea":
                    return isPrimaryKey ? "VARBINARY(255)" : "VARBINARY(MAX)";
                default:
                    return isPrimaryKey ? "VARCHAR(255)" : "VARCHAR(MAX)";
            }
        }
        public class ForeignKeyInfo
        {
            public string ForeignKeyName { get; set; }
            public string SchemaName { get; set; }
            public string ParentTable { get; set; }
            public string ParentColumn { get; set; }
            public string ReferencedTable { get; set; }
            public string ReferencedColumn { get; set; }
        }
        #endregion

    }
}
