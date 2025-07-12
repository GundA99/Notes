namespace DBMigrator.DBMigrator.Dto
{
    public class Authentication
    {
        public string? SqlServerName { get; set; }   
        public string? ngServerName { get; set; }   
    }

    public class MigrationRequest
    {
        public string SqlConnection { get; set; }
        public string PgConnection { get; set; }
        public string DbName { get; set; }
        public List<string>? SchemaList { get; set; }
    }

}
