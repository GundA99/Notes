using DBMigrator.DBMigrator.Dto;
using DBMigratorBLogic.Migrator;
using Microsoft.AspNetCore.Mvc;

namespace DBMigrator.DBMigrator
{
    [Route("api/[Controller]")]
    public class MigratorAppService: ControllerBase
    {
        private readonly IMigratorBLogic _migratorBLogic;
        public MigratorAppService(IMigratorBLogic migratorBLogic)
        {
            _migratorBLogic = migratorBLogic;   
        }

        [HttpGet("MigrateDataOnly")]
        public IActionResult MigrateDataOnly()
        {
            _migratorBLogic.MigrateDataOnly();
            return Ok("Data added successfully !");
        }

        [HttpGet("MigrateSchemaAndTables")]
        public IActionResult MigrateSchemaAndTables()
        {
            _migratorBLogic.MigrateSchemaAndTables();
            return Ok("Schema and tables added");
        }

        [HttpGet("CopyStoreProcedure")]
        public void CopyStoreProcedure()
        {
            _migratorBLogic.CopyStoredProcedures();
        }

        [HttpGet("MigrateForeignKeys")]
        public void MigrateForeignKeys()
        {
            _migratorBLogic.MigrateForeignKeys();
        }

        [HttpPost("Login")]
        public List<string> Login([FromBody] Authentication authentication)
        {
            return _migratorBLogic.Login(authentication);
        }        
    }
}
