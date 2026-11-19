using System.Globalization;
using Voicer.Localization;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// A voice shipped with a model in the list of voices: its name, accent and kind, worded from the name the engine takes
/// (<c>af_heart</c>: American English, female, Heart).
/// </summary>
public sealed class VoiceOptionViewModel : LocalizedViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceOptionViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="id">The name of the voice the engine takes.</param>
    public VoiceOptionViewModel(ILocalizer localizer, string id)
        : base(localizer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    /// <summary>
    /// Gets the name of the voice the engine takes.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the voice as it is shown: <c>Heart · American English, female</c>; a name of another form as it is.
    /// </summary>
    public string Title
    {
        get
        {
            // The form of Kokoro: a letter of the accent, a letter of the kind, an underscore and the name.
            if (Id.Length < 4 || Id[2] != '_' || Id[0] is not ('a' or 'b') || Id[1] is not ('f' or 'm'))
            {
                return Id;
            }

            var name = Id[3..];
            name = char.ToUpper(name[0], CultureInfo.InvariantCulture) + name[1..];

            return Localizer.Format(
                "Voice.Option",
                name,
                Localizer.Get("Voice.Accent." + Id[0]),
                Localizer.Get("Voice.Kind." + Id[1]));
        }
    }
}
