using Voicer.Localization;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// A language a voice model speaks, in the list of languages.
/// </summary>
public sealed class SpeechLanguageViewModel : LocalizedViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpeechLanguageViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="code">The code of the language (<c>en</c>, <c>ru</c>).</param>
    public SpeechLanguageViewModel(ILocalizer localizer, string code)
        : base(localizer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>
    /// Gets the code of the language.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the name of the language in the language of the interface.
    /// </summary>
    public string Name => Localizer.Get("SpeechLanguage." + Code);
}
