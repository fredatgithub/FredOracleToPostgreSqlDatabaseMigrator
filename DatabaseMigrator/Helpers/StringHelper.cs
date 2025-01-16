namespace DatabaseMigrator.Helpers
{
  public static class StringHelper
  {
    public static string Plural(int count)
    {
      return count > 1 ? "s" : "";
    }
  }
}
