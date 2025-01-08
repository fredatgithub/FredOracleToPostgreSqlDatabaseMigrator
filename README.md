# Fred's Oracle to PostgreSQL Database Migrator

This WPF application is a tool that allows you to migrate data from an Oracle database to a PostgreSQL database. 

Here is an example of how to use the application:

1. Enter the connection details for the Oracle and PostgreSQL databases.
2. Click the "Test Oracle Connection" button to verify that you can connect to the Oracle database.
3. Click the "Test PostgreSQL Connection" button to verify that you can connect to the PostgreSQL database.
4. Click the "Load Oracle Tables" button to load the tables from the Oracle database into the PostgreSQL database.
5. Click the "Load PostgreSQL Tables" button to load the tables from the PostgreSQL database into the Oracle database.

This application uses the Oracle and Npgsql libraries to connect to the databases and the System.Data.DataTable class to load the data from the Oracle tables into the PostgreSQL tables.

The application also uses the Newtonsoft.Json library to serialize and deserialize the data from the Oracle tables into the PostgreSQL tables.

The application is written in C# using the WPF framework.

The application is open source and available on GitHub.

The application is licensed under the MIT license and is free to use for any purpose. It is copyright (c) 2025 by Freddy Juhel.
