using System.Collections.Generic;

namespace DatabaseMigrator
{
    public interface IPostgresService
    {
        List<string> GetStoredProcedures();
    }
} 