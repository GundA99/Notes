using DBMigrator.DBMigrator.Dto;
using DBMigratorBLogic.Migrator;
using Microsoft.AspNetCore.Mvc;

namespace DBMigrator.DBMigrator
{
    [Route("api/[Controller]")]
    public class MigratorAppService : ControllerBase
    {
        private readonly IMigratorBLogic _migratorBLogic;
        public MigratorAppService(IMigratorBLogic migratorBLogic)
        {
            _migratorBLogic = migratorBLogic;
        }
        [HttpPost("Login")]
        public Dictionary<string, List<string>> Login([FromBody] Authentication authentication)
        {
            return _migratorBLogic.Login(authentication);
        }

        [HttpGet("MigrateDataOnlyFromSqlServerToPostgre")]
        public IActionResult MigrateDataOnly(List<string>? schemaList)
        {
            _migratorBLogic.MigrateDataOnly(schemaList);
            return Ok("Data added successfully !");
        }

        [HttpGet("MigrateSchemaAndTablesServerToPostgre")]
        public IActionResult MigrateSchemaAndTables(List<string>? schemaList)
        {
            _migratorBLogic.MigrateSchemaAndTables(schemaList);
            return Ok("Schema and tables added");
        }

        //[HttpGet("CopyStoreProcedure")]
        //public void CopyStoreProcedure()
        //{
        //    _migratorBLogic.CopyStoredProcedures();
        //}

        [HttpGet("MigrateForeignKeysServerToPostgre")]
        public IActionResult MigrateForeignKeys(List<string>? schemaList)
        {
            _migratorBLogic.MigrateForeignKeys(schemaList);
            return Ok("All Constrain Added Successfully");
        }

        [HttpPost("MigrateSchemaAndTablesPostgreToSqlServer")]
        public IActionResult MigrateSchemaAndTablesFromPostgreToSqlServer([FromBody] List<string>? schemaList)
        {
            _migratorBLogic.MigrateSchemaAndTablesFromPostgreToSqlServer(schemaList);
            return Ok("Schema and tables added");
        }

        [HttpPost("MigrateDataOnlyFromPostgreToSqlServer")]
        public IActionResult MigrateDataOnlyFromPostgreToSqlServer([FromBody] List<string>? schemaList)
        {
            _migratorBLogic.MigrateDataOnlyFromPostgreToSqlServer(schemaList);
            return Ok("Data added successfully !");
        }

        [HttpPost("MigrateForeignKeysFromPostgreToSqlServer")]
        public IActionResult MigrateForeignKeysFromPostgreToSqlServer([FromBody] List<string>? schemaList)
        {
            _migratorBLogic.MigrateForeignKeysFromPostgreToSqlServer(schemaList);
            return Ok("All Constrain Added Successfully");
        }
        
        [HttpPost("MigrateSchemaAndTablesUI")]
        public IActionResult MigrateSchemaAndTablesUI([FromBody] MigrationRequest request)
        {
            _migratorBLogic.MigrateSchemaAndTablesUI(request.SqlConnection, request.PgConnection, request.DbName, request.SchemaList);
             return Ok(new { message = "Data added successfully!" });
        }

        [HttpPost("MigrateDataOnlySQLToPgUI")]
        public IActionResult MigrateDataOnlyUI([FromBody] MigrationRequest request)
        {
             _migratorBLogic.MigrateDataOnlyUI(request.SqlConnection, request.PgConnection, request.DbName, request.SchemaList);
            return Ok(new { message = "Data added successfully!" });
        }
    }
}