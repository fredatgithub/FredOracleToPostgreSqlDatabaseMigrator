namespace DatabaseMigrator
{
    public class PostgresService : IPostgresService
    {
        public List<string> GetStoredProcedures()
        {
            // Implémentez la logique pour récupérer les procédures stockées
            return new List<string> { "Procedure1", "Procedure2" }; // Exemple
        }
    }
} 