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


        public void MigrateDataOnly()
        {
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
                if (PostGryschema.Contains(schema))
                {
                    var Sqltables = SqlConnection.Query<string>(
                        $"SELECT table_name " +
                        $" FROM information_schema.tables " +
                        $" WHERE table_schema = '{schema}' AND table_type = 'BASE TABLE'"
                    ).ToList();

                    //if (schema != "Base")
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
        public void MigrateSchemaAndTables()
        {
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
                //if (schema == "Base")
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
        public void MigrateForeignKeys()
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

        public List<string> Login(Authentication authentication)
        {
            string SqlConnectionString = $"Server={authentication.SqlServerName}; User={authentication.SqlUserName}; Password={authentication.SqlPassword}; Trusted_Connection=False; TrustServerCertificate=True;";
            var postgryConnectionString = $"Server={authentication.ngServerName};Port={authentication.ngPort};UserId={authentication.ngSqlUserId};Password={authentication.ngSqlPassword}";

            //var postgryConnection = new NpgsqlConnection(postgryConnectionString);
            var SqlConnection = new SqlConnection(SqlConnectionString);

            //postgryConnection.Open();
            try
            {
                SqlConnection.Open();
                
                var DatabaseList = SqlConnection.Query<string>(
                                    @"SELECT name 
                                    FROM sys.databases
                                    WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')" 
                                  ).ToList();
                return DatabaseList;
            }
            catch(SqlException ex)
            {
                throw new Exception("SQL Connection failed.");
            }
            catch(PostgresException ex)
            {
                throw new Exception("Postgres Connection failed.");
            }           
            finally
            {
                SqlConnection.Close();
                //postgryConnection.Open();
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

        #endregion

    }
}
