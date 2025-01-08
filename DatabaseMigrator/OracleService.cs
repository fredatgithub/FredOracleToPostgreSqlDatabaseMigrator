using System.Collections.Generic;

public class OracleService : IOracleService
{
    public List<string> GetStoredProcedures()
    {
        return new List<string> { "Procedure1", "Procedure2" };
    }
} 