using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using Xunit;

namespace ReactPosApi.UnitTests.SYST;

/// <summary>Constitution I (database naming) and II (module codes)</summary>
public class NamingConventionTests
{
    public static readonly string[] ModuleCodes = { "SYST", "SECU", "PRTY", "INVT", "CATL", "SALE", "PRCH", "FINC", "ACCT", "RPRT", "AGNT", "MESG" };
    private static readonly Regex Name = new(DbNames.Pattern);
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", ".."));

    private static IEnumerable<string> AllConstants() =>
        typeof(DbNames).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != nameof(DbNames.Pattern))
            .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void Every_database_name_is_4_letter_segments_joined_by_underscore()
    {
        var bad = AllConstants().Concat(DbNames.Tables.Values).Where(n => !Name.IsMatch(n)).ToList();
        Assert.Empty(bad);
    }

    [Fact]
    public void Every_name_starts_with_a_module_code()
    {
        var bad = AllConstants().Where(n => !ModuleCodes.Contains(n[..4])).ToList();
        Assert.Empty(bad);
    }

    [Fact]
    public void Views_and_procedures_have_VIEW_or_PROC_as_second_segment()
    {
        foreach (var f in typeof(DbNames).GetFields().Where(f => f.IsLiteral && f.Name.Contains("View")))
            Assert.Equal("VIEW", ((string)f.GetRawConstantValue()!).Split('_')[1]);
        foreach (var f in typeof(DbNames).GetFields().Where(f => f.IsLiteral && f.Name.Contains("Proc")))
            Assert.Equal("PROC", ((string)f.GetRawConstantValue()!).Split('_')[1]);
    }

    [Fact]
    public void Table_names_are_unique()
    {
        var names = DbNames.Tables.Values.ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_entity_of_the_model_is_mapped_to_its_registered_table()
    {
        using var t = new TestDb();
        foreach (var entity in t.Db.Model.GetEntityTypes())
        {
            Assert.True(DbNames.Tables.TryGetValue(entity.ClrType, out var table), $"{entity.ClrType.Name} has no name in DbNames.Tables");
            Assert.Equal(table, entity.GetTableName());
        }
        Assert.Equal(DbNames.Tables.Count, t.Db.Model.GetEntityTypes().Count());
    }

    [Fact]
    public void Every_legacy_name_maps_to_a_registered_table_and_every_table_has_one()
    {
        Assert.All(DbNames.Legacy.Values, v => Assert.Contains(v, DbNames.Tables.Values));
        Assert.Equal(DbNames.Tables.Values.OrderBy(x => x), DbNames.Legacy.Values.OrderBy(x => x));
    }

    [Fact]
    public void Sql_scripts_only_create_conforming_tables_views_and_procedures()
    {
        var sqlFiles = Directory.GetFiles(Path.Combine(RepoRoot(), "Database"), "*.sql", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}"));
        var created = new Regex(@"CREATE\s+(?:TABLE\s+IF\s+NOT\s+EXISTS|OR\s+REPLACE\s+VIEW|PROCEDURE)\s+`?(\w+)`?", RegexOptions.IgnoreCase);
        var names = sqlFiles.SelectMany(f => created.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value)).ToList();
        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.Matches(DbNames.Pattern, n));
    }

    [Theory]
    [InlineData("Controllers")]
    [InlineData("Services")]
    [InlineData("Models")]
    [InlineData("DTOs")]
    public void Backend_code_lives_in_module_folders(string layer)
    {
        var dir = Path.Combine(RepoRoot(), layer);
        Assert.Empty(Directory.GetFiles(dir, "*.cs"));                                   // nothing at the top of a layer
        Assert.All(Directory.GetDirectories(dir).Select(Path.GetFileName), d => Assert.Contains(d, ModuleCodes));
    }

    [Fact]
    public void Database_scripts_live_in_module_folders()
    {
        var dirs = Directory.GetDirectories(Path.Combine(RepoRoot(), "Database")).Select(Path.GetFileName)
            .Where(d => d is not ("Migrations" or "tests"));
        Assert.All(dirs, d => Assert.Contains(d, ModuleCodes));
    }

    [Fact]
    public void Embedded_scripts_run_in_dependency_order()
    {
        Assert.Equal(new[] { "SYST/Procedures.sql", "CATL/Schema.sql", "ACCT/Schema.sql" }, DatabaseSetup.Scripts);
        var names = typeof(DatabaseSetup).Assembly.GetManifestResourceNames();
        Assert.All(DatabaseSetup.Scripts, s => Assert.Contains(s, names));
    }

    [Theory]
    [InlineData("Sales", true)]
    [InlineData("sales", true)]                 // lower_case_table_names = 1
    [InlineData("JournalLines", true)]
    [InlineData("SALE_HEAD", false)]
    [InlineData("acct_detl", false)]
    [InlineData("SomethingElse", false)]
    public void Legacy_table_names_are_recognised_in_any_case(string table, bool legacy)
    {
        Assert.Equal(legacy, DatabaseSetup.FindLegacy(new[] { table }).Count == 1);
    }
}
