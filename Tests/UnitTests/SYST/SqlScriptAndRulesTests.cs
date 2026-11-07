using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;

namespace ReactPosApi.UnitTests.SYST;

/// <summary>Script splitting (Data/SqlScript.cs) and the purchase payment rules (Services/PRCH/PurchasePaymentRules.cs)</summary>
public class SqlScriptAndRulesTests
{
    [Fact]
    public void Script_is_split_into_statements_and_DELIMITER_blocks_stay_whole()
    {
        var sql = "CREATE TABLE a (x int);\n-- note\nDROP PROCEDURE IF EXISTS p;\nDELIMITER $$\nCREATE PROCEDURE p() BEGIN SELECT 1; SELECT 2; END$$\nDELIMITER ;\nSELECT 3;";
        var parts = SqlScript.Split(sql).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        Assert.Equal(4, parts.Count);
        Assert.Contains(parts, p => p.StartsWith("CREATE PROCEDURE p()") && p.Contains("SELECT 1; SELECT 2;"));
        Assert.DoesNotContain(parts, p => p.Contains("DELIMITER"));
    }

    [Fact]
    public void Embedded_module_scripts_split_without_leftovers()
    {
        foreach (var name in DatabaseSetup.Scripts)
        {
            using var s = typeof(DatabaseSetup).Assembly.GetManifestResourceStream(name)!;
            var parts = SqlScript.Split(new StreamReader(s).ReadToEnd()).ToList();
            Assert.NotEmpty(parts);
            Assert.DoesNotContain(parts, p => p.Contains("DELIMITER"));
        }
    }

    private static PurchasePayment P(decimal a, string type = PurchasePaymentType.Payment, string method = "Cash") => new() { Amount = a, Type = type, Method = method };

    [Fact]
    public void Paid_is_payments_minus_refunds_and_cash_paid_leaves_out_return_credits()
    {
        var records = new[] { P(500), P(100, PurchasePaymentType.Refund), P(200, method: PurchasePaymentRules.ReturnCredit) };
        Assert.Equal(600, PurchasePaymentRules.NetPaid(records));
        Assert.Equal(400, PurchasePaymentRules.CashPaid(records));
    }

    [Theory]
    [InlineData(0, 100, "Unpaid")]
    [InlineData(-5, 100, "Unpaid")]
    [InlineData(40, 100, "Partial")]
    [InlineData(100, 100, "Paid")]
    [InlineData(120, 100, "Overpaid")]
    public void Payment_status(decimal paid, decimal total, string status) => Assert.Equal(status, PurchasePaymentRules.Status(paid, total));
}
