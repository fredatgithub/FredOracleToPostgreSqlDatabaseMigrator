using System.Collections.Generic;
using DatabaseMigrator.Models;

namespace DatabaseMigrator
{
  public interface IOracleService
  {
    List<OracleProcStockUnit> GetStoredProcedures();
  }
}