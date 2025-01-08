namespace DatabaseMigrator
{
    public interface IOracleService
    {
        List<string> GetStoredProcedures();
    }
} 