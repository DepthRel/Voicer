namespace Voicer.UI.Services.Configuration;

/// <summary>
/// Remembers what the user chose for summaries.
/// </summary>
public interface ISummaryPreferencesStore
{
    /// <summary>
    /// Reads the choice; the defaults stand in for what was never chosen.
    /// </summary>
    /// <returns>The choice.</returns>
    SummaryPreferences Load();

    /// <summary>
    /// Remembers the choice.
    /// </summary>
    /// <param name="preferences">The choice.</param>
    void Save(SummaryPreferences preferences);
}
