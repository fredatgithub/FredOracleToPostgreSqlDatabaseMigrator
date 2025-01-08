using System.Collections.Generic;

public class OracleService : IOracleService
{
    public List<string> GetStoredProcedures()
    {
        // Implémentez la logique pour récupérer les procédures stockées
        return new List<string> { "Procedure1", "Procedure2" }; // Exemple
    }
} 