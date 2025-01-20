using System.Collections.Generic;
using Npgsql;
using System.Data;

namespace DatabaseMigrator
{
  public class PostgresService : IPostgresService
  {
    private readonly string _connectionString;
    private readonly string _schema;

    public PostgresService(string connectionString, string schema)
    {
      _connectionString = connectionString;
      _schema = schema?.ToLower();
    }

    public List<string> GetStoredProcedures()
    {
      var procedures = new List<string>();

      using (var connection = new NpgsqlConnection(_connectionString))
      {
        connection.Open();

        // Requête pour obtenir toutes les procédures stockées du schéma spécifié
        var query = @"
          SELECT DISTINCT p.proname
          FROM pg_proc p
          JOIN pg_namespace n ON p.pronamespace = n.oid
          WHERE n.nspname = @schema
          AND p.prokind = 'p'
          ORDER BY p.proname";

        using (var command = new NpgsqlCommand(query, connection))
        {
          command.Parameters.AddWithValue("schema", _schema);

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