using System.Globalization;
using Aegi.Vas.Dashboard.Api.Contracts;
using System.Text.RegularExpressions;

namespace Aegi.Vas.Dashboard.Api.Reporting;

/// <summary>Validated reporting filter. Months are inclusive, formatted yyyy-MM.</summary>
public sealed record ReportFilter(string From, string To, string? Country, string? Centre);

public static partial class ReportFilterValidator
{
    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex MonthPattern();

    /// <summary>
    /// Checks shape and checks country/centre against values that exist in the reporting data.
    /// A valid filter narrows the result; it is never treated as proof of permission.
    /// </summary>
    public static Dictionary<string, string[]> Validate(
        string from, string to, string? country, string? centre, FilterOptions options)
    {
        var errors = new Dictionary<string, string[]>();
        if (!MonthPattern().IsMatch(from)) errors["from"] = ["Use the format yyyy-MM, for example 2026-01."];
        if (!MonthPattern().IsMatch(to)) errors["to"] = ["Use the format yyyy-MM, for example 2026-09."];
        if (errors.Count == 0 && string.CompareOrdinal(from, to) > 0)
            errors["from"] = ["'from' must be on or before 'to'."];
        if (errors.Count == 0 && MonthsBetween(from, to) > 36)
            errors["to"] = ["The period can be at most 36 months."];

        CountryOption? match = null;
        if (country is not null)
        {
            match = options.Countries.FirstOrDefault(c => c.Country.Equals(country, StringComparison.OrdinalIgnoreCase));
            if (match is null) errors["country"] = ["Unknown country."];
        }
        if (centre is not null)
        {
            if (country is null) errors["centre"] = ["Choose a country before a centre."];
            else if (match is not null && !match.Centres.Contains(centre, StringComparer.OrdinalIgnoreCase))
                errors["centre"] = ["Unknown centre for this country."];
        }
        return errors;
    }

    private static int MonthsBetween(string from, string to)
    {
        var a = DateTime.ParseExact(from, "yyyy-MM", CultureInfo.InvariantCulture);
        var b = DateTime.ParseExact(to, "yyyy-MM", CultureInfo.InvariantCulture);
        return (b.Year - a.Year) * 12 + b.Month - a.Month + 1;
    }
}
