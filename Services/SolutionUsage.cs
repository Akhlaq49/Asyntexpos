using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// Which packages a product may go in, from its ticks in the product categorization:
///  - Standard only -> only Standard packages
///  - Premium only  -> only Premium packages
///  - both          -> Standard packages straight away; Premium packages only after the user confirms it
///  - neither / not categorised -> no package
/// </summary>
public static class SolutionUsage
{
    public const string Standard = "standard";
    public const string Premium = "premium";
    public const string Both = "both";
    public const string None = "none";

    public static string Of(SolutionProductLine? line) => line switch
    {
        null => None,
        { InStandard: true, InPremium: true } => Both,
        { InStandard: true } => Standard,
        { InPremium: true } => Premium,
        _ => None,
    };

    public static bool IsSolution(string? solution) => solution is Standard or Premium;

    /// <summary>Goes in the package without asking</summary>
    public static bool Included(string usage, string solution) =>
        usage == solution || (usage == Both && solution == Standard);

    /// <summary>The user is asked whether to use it (only "both" products, only when designing a Premium package)</summary>
    public static bool NeedsConfirmation(string usage, string solution) => usage == Both && solution == Premium;
}
