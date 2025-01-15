using System.Collections.Generic;
using DatabaseMigrator.Models;

namespace DatabaseMigrator
{
  internal class MigrationWindow
  {
    private List<TableInfo> _selectedOracleTables;
    private List<TableInfo> _selectedPostgresTables;
    private string _sourceConnectionString;
    private string _targetConnectionString;

    public MigrationWindow(List<TableInfo> selectedOracleTables, List<TableInfo> selectedPostgresTables, string sourceConnectionString, string targetConnectionString)
    {
      _selectedOracleTables = selectedOracleTables;
      _selectedPostgresTables = selectedPostgresTables;
      _sourceConnectionString = sourceConnectionString;
      _targetConnectionString = targetConnectionString;
    }
  }
}