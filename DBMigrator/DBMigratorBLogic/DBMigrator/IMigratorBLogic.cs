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
        public void MigrateDataOnly();
        public void MigrateSchemaAndTables();
        public void CopyStoredProcedures();
        public void MigrateForeignKeys();
        public List<string> Login(Authentication ?authentication );
    }
}
