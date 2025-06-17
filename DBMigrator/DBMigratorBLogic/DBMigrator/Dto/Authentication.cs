namespace DBMigrator.DBMigrator.Dto
{
    public class Authentication
    {
        public string? SqlServerName { get; set; }
        public string? SqlUserName { get; set; }
        public string? SqlPassword { get; set; }
        public string? ngServerName { get; set; }
        public string? ngSqlUserId { get; set; }
        public string? ngSqlPassword { get; set; }
        public string? ngPort { get; set; }
    }
}
