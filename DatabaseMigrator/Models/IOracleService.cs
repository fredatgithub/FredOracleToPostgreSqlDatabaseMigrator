using System.Collections.Generic;

namespace DatabaseMigrator
{
    public interface IOracleService
    {
        List<string> GetStoredProcedures();
    }
} 