using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ReactPosApi.Data;

/// <summary>
/// Runs a .sql file the way SQL Server Management Studio / sqlcmd does: the file is cut into batches at the lines
/// that hold only "GO", and the batches run one after another on one connection.
/// </summary>
public static class SqlScript
{
    public static void RunResource(string connectionString, string resourceName)
    {
        using var stream = typeof(SqlScript).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL script {resourceName} not found.");
        using var reader = new StreamReader(stream);
        Run(connectionString, reader.ReadToEnd());
    }

    public static void Run(string connectionString, string sql)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        foreach (var batch in Split(sql))
        {
            using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 300 };
            cmd.ExecuteNonQuery();
        }
    }

    private static readonly Regex Go = new(@"^\s*GO\s*(--.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>The batches of the script (lines of only "GO" separate them); batches with nothing but comments are left out</summary>
    public static IEnumerable<string> Split(string sql)
    {
        var current = new StringBuilder();
        foreach (var line in sql.Replace("\r\n", "\n").Split('\n'))
        {
            if (Go.IsMatch(line))
            {
                if (HasCode(current)) yield return current.ToString().Trim();
                current.Clear();
                continue;
            }
            current.AppendLine(line.TrimEnd());
        }
        if (HasCode(current)) yield return current.ToString().Trim();
    }

    private static bool HasCode(StringBuilder batch) =>
        batch.ToString().Split('\n').Any(l => l.Trim().Length > 0 && !l.Trim().StartsWith("--"));
}
