using System.Collections.Generic;

namespace DatabaseMigrator
{
  public class PostgresService : IPostgresService
  {
    public List<string> GetStoredProcedures()
    {
      return new List<string> { "Procedure1", "Procedure2" };
    }
  }
}