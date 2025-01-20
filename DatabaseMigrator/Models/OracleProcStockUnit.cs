using System.Collections.Generic;

namespace DatabaseMigrator.Models
{
  public class OracleProcStockUnit
  {
    public string Name { get; set; }
    public string Type { get; set; }
    public List<string> PackageProcedures { get; set; }
  }
}
