using System.Text;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace ReactPosApi.Data;

/// <summary>
/// Runs a .sql file the way the mysql command line does: statements end with the current delimiter, and
/// "DELIMITER $$" changes it (for procedures). One connection for the whole file, so temporary tables and
/// transactions work; user variables (@x) are allowed.
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
        var csb = new MySqlConnectionStringBuilder(connectionString) { AllowUserVariables = true };
        using var conn = new MySqlConnection(csb.ConnectionString);
        conn.Open();
        foreach (var statement in Split(sql))
        {
            using var cmd = new MySqlCommand(statement, conn) { CommandTimeout = 300 };
            cmd.ExecuteNonQuery();
        }
    }

    private static readonly Regex TrailingComment = new(@"\s--\s.*$", RegexOptions.Compiled);

    public static IEnumerable<string> Split(string sql)
    {
        var delimiter = ";";
        var current = new StringBuilder();
        foreach (var raw in sql.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.Trim();
            if (trimmed.StartsWith("DELIMITER ", StringComparison.OrdinalIgnoreCase))
            {
                delimiter = trimmed[10..].Trim();
                continue;
            }
            if (current.Length == 0 && (trimmed.Length == 0 || trimmed.StartsWith("--"))) continue;
            current.AppendLine(line);
            var code = TrailingComment.Replace(trimmed, "").TrimEnd();
            if (!code.EndsWith(delimiter)) continue;
            var text = current.ToString().TrimEnd();
            text = TrailingComment.Replace(text, "").TrimEnd();
            if (text.EndsWith(delimiter)) text = text[..^delimiter.Length];
            current.Clear();
            if (text.Trim().Length > 0) yield return text.Trim();
        }
        if (current.ToString().Trim().Length > 0) yield return current.ToString().Trim();
    }
}
