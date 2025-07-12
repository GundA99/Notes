using DBMigrator.DBMigrator.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBMigratorBLogic.Migrator
{
    public interface IMigratorBLogic
    {
        public void MigrateDataOnly(List<string>? schemaList);
        public void MigrateSchemaAndTables(List<string>? schemaList);
        public void CopyStoredProcedures();
        public void MigrateForeignKeys(List<string>? schemaList);
        public Dictionary<string, List<string>> Login(Authentication authentication);
        public void MigrateSchemaAndTablesFromPostgreToSqlServer(List<string>? schemaList);
        public void MigrateDataOnlyFromPostgreToSqlServer(List<string>? schemaList);
        public void MigrateForeignKeysFromPostgreToSqlServer(List<string>? schemaList);
        public void MigrateSchemaAndTablesUI(string sqlBaseConnectionString, string pgBaseConnectionString, string SqldbName, List<string>? schemaList);
        public void MigrateDataOnlyUI(string sqlBaseConnectionString, string pgBaseConnectionString, string SqldbName, List<string>? schemaList);


    }
}
