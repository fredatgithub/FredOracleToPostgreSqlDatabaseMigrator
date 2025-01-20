using System.Collections.Generic;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System;
using System.Linq;

namespace DatabaseMigrator
{
  public class OracleService : IOracleService
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

    public List<string> GetStoredProcedures()
    {
      var procedures = new List<string>();
      
      using (var connection = new OracleConnection(_connectionString))
      {
        connection.Open();

        // Requête pour obtenir toutes les procédures stockées
        var query = @"
          SELECT DISTINCT OBJECT_NAME 
          FROM ALL_PROCEDURES 
          WHERE OWNER = :owner 
          AND OBJECT_TYPE = 'PROCEDURE'
          ORDER BY OBJECT_NAME";

        using (var command = new OracleCommand(query, connection))
        {
          command.Parameters.Add(new OracleParameter("owner", _owner));

          using (var reader = command.ExecuteReader())
          {
            while (reader.Read())
            {
              procedures.Add(reader.GetString(0));
            }
          }
        }
      }

      return procedures;
    }
  }
}