using System;

namespace DatabaseMigrator.Models
{
    public class DbCredentials
    {
        public string Server { get; set; }
        public string Port { get; set; }
        public string Database { get; set; }
        public string Schema { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
