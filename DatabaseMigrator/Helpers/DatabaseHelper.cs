using NpgsqlTypes;
using System;

namespace DatabaseMigrator.Helpers
{
  public static class DatabaseHelper
  {
    public static NpgsqlDbType GetNpgsqlType(Type type)
    {
      if (type == typeof(int) || type == typeof(Int32))
      {
        return NpgsqlDbType.Integer;
      }

      if (type == typeof(long) || type == typeof(Int64))
      {
        return NpgsqlDbType.Bigint;
      }

      if (type == typeof(string) || type == typeof(char))
      {
        return NpgsqlDbType.Text;
      }

      if (type == typeof(DateTime))
      {
        return NpgsqlDbType.Timestamp;
      }

      if (type == typeof(bool))
      {
        return NpgsqlDbType.Boolean;
      }

      if (type == typeof(byte[]))
      {
        return NpgsqlDbType.Bytea;
      }

      if (type == typeof(float) || type == typeof(Single))
      {
        return NpgsqlDbType.Real;
      }

      if (type == typeof(double))
      {
        return NpgsqlDbType.Double;
      }

      if (type == typeof(decimal))
      {
        return NpgsqlDbType.Numeric;
      }

      if (type == typeof(short) || type == typeof(Int16))
      {
        return NpgsqlDbType.Smallint;
      }

      if (type == typeof(byte))
      {
        return NpgsqlDbType.Smallint;
      }

      if (type == typeof(Guid))
      {
        return NpgsqlDbType.Uuid;
      }

      if (type == typeof(TimeSpan))
      {
        return NpgsqlDbType.Interval;
      }

      if (type == typeof(DateTimeOffset))
      {
        return NpgsqlDbType.TimestampTz;
      }

      return NpgsqlDbType.Unknown;
    }
  }
}
