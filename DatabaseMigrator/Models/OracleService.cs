using System;
using System.Collections.Generic;
using System.Linq;
using DatabaseMigrator.Models;
using Oracle.ManagedDataAccess.Client;

namespace DatabaseMigrator
{
  public class OracleService: IOracleService
  {
    private readonly string _connectionString;
    private readonly string _owner;

    public OracleService(string connectionString)
    {
      _connectionString = connectionString;
      _owner = ExtractOwnerFromConnectionString(connectionString);
    }

    private string ExtractOwnerFromConnectionString(string connectionString)
    {
      try
      {
        var builder = new OracleConnectionStringBuilder(connectionString);
        return builder.UserID?.ToUpper();
      }
      catch (Exception)
      {
        // En cas d'erreur, on essaie de parser manuellement
        var userIdPart = connectionString.Split(';')
            .FirstOrDefault(p => p.Trim().StartsWith("User Id=", StringComparison.OrdinalIgnoreCase));

        if (userIdPart != null)
        {
          return userIdPart.Split('=')[1].Trim().ToUpper();
        }

        throw new ArgumentException("Unable to extract owner from connection string. Please ensure User Id is specified.");
      }
    }

    public List<OracleProcStockUnit> GetStoredProcedures()
    {
      var procedures = new List<OracleProcStockUnit>();

      using (var connection = new OracleConnection(_connectionString))
      {
        connection.Open();

        // Requête pour obtenir toutes les procédures stockées, fonctions et packages
        var query = @"
          SELECT OBJECT_NAME, OBJECT_TYPE, 
                 CASE 
                   WHEN OBJECT_TYPE = 'PACKAGE' THEN 
                     (SELECT LISTAGG(PROCEDURE_NAME, ',') WITHIN GROUP (ORDER BY PROCEDURE_NAME)
                      FROM ALL_PROCEDURES 
                      WHERE OWNER = p.OWNER 
                      AND OBJECT_NAME = p.OBJECT_NAME
                      AND PROCEDURE_NAME IS NOT NULL)
                   ELSE NULL 
                 END AS PACKAGE_PROCEDURES
          FROM ALL_OBJECTS p
          WHERE OWNER = :owner 
          AND OBJECT_TYPE IN ('PROCEDURE', 'FUNCTION', 'PACKAGE')
          AND STATUS = 'VALID'
          ORDER BY 
            CASE OBJECT_TYPE 
              WHEN 'PACKAGE' THEN 1
              WHEN 'PROCEDURE' THEN 2
              WHEN 'FUNCTION' THEN 3
              ELSE 4
            END,
            OBJECT_NAME";

        using (var command = new OracleCommand(query, connection))
        {
          command.Parameters.Add(new OracleParameter("owner", _owner));

          using (var reader = command.ExecuteReader())
          {
            while (reader.Read())
            {
              var name = reader.GetString(0);
              var type = reader.GetString(1);
              var packageProcedures = reader.IsDBNull(2) ? null : reader.GetString(2);

              procedures.Add(new OracleProcStockUnit
              {
                Name = name,
                Type = type,
                PackageProcedures = packageProcedures?.Split(',').ToList()
              });
            }
          }
        }
      }

      return procedures;
    }
  }
}