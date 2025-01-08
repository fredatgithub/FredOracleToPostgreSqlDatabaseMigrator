namespace DatabaseMigrator
{
    public interface IPostgresService
    {
        List<string> GetStoredProcedures();
    }
} 