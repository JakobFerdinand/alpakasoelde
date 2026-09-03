using System.Globalization;

namespace DashboardApi.Features.Gutscheine;

/// <summary>
/// Kaufdatum and Einlösedatum are calendar days, not instants: the dashboard sends
/// them as bare <c>yyyy-MM-dd</c> from a date input.
/// </summary>
/// <remarks>
/// Parsing has to pin the day explicitly. Plain <c>DateTimeOffset.Parse</c> assigns
/// the host's local offset to a value that carries none, and <c>DateTimeOffset.Date</c>
/// then re-attaches that offset — so on a host that is not UTC the same input is
/// stored, compared and rendered as a different day. The culture is pinned for the
/// same reason.
/// </remarks>
public static class GutscheinDates
{
    // AssumeUniversal fixes the offset of a bare date at UTC instead of the host's.
    // AdjustToUniversal is deliberately not set: a value that does carry an offset
    // keeps the day it was written as rather than being shifted into another one.
    private const DateTimeStyles Styles = DateTimeStyles.AssumeUniversal;

    public static bool TryParseDate(string? value, out DateTimeOffset date)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, Styles, out DateTimeOffset parsed))
        {
            date = default;
            return false;
        }

        date = ToDay(parsed);
        return true;
    }

    /// <summary>Reduces a stored value to the day it stands for, dropping its offset.</summary>
    /// <remarks>Rows written before this was pinned carry the offset of the host that wrote them.</remarks>
    public static DateTimeOffset ToDay(DateTimeOffset value) => new(value.Date, TimeSpan.Zero);
}
