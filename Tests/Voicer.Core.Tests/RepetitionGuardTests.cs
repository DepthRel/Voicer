using FluentAssertions;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the guard against the loops of a speech model.
/// </summary>
public sealed class RepetitionGuardTests
{
    /// <summary>
    /// A sentence the model repeats for every following window is kept once, whatever its case and punctuation.
    /// </summary>
    [Fact]
    public void Accept_DropsALoopOfOneSentence()
    {
        var guard = new RepetitionGuard();
        var kept = new[]
        {
            "В основном, это связано с увеличением тока короткого замыкания.",
            "В основном, это связано с увеличением тока короткого замыкания.",
            " в основном это связано с увеличением тока короткого замыкания ",
            "В основном, это связано с увеличением тока короткого замыкания!",
        }.Count(text => guard.Accept(Piece(text)));

        kept.Should().Be(1);
    }

    /// <summary>
    /// A loop of two sentences taking turns is caught too.
    /// </summary>
    [Fact]
    public void Accept_DropsALoopOfTwoSentences()
    {
        var guard = new RepetitionGuard();

        var kept = new[] { "Первая фраза из лекции.", "Вторая фраза из лекции.", "Первая фраза из лекции.", "Вторая фраза из лекции." }
            .Count(text => guard.Accept(Piece(text)));

        kept.Should().Be(2);
    }

    /// <summary>
    /// Short answers may repeat, a sentence may come back after other speech, and different sentences all stay.
    /// </summary>
    [Fact]
    public void Accept_KeepsRealSpeech()
    {
        var guard = new RepetitionGuard();
        string[] speech =
        [
            "Да.", "Да.", "Спасибо большое.", "Спасибо большое.",
            "Итак, начнём с первого вопроса.", "Ток растёт.", "Напряжение падает.", "Сопротивление постоянно.",
            "Итак, начнём с первого вопроса.",
        ];

        speech.Count(text => guard.Accept(Piece(text))).Should().Be(speech.Length);
    }

    /// <summary>
    /// Creates a piece of speech with a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The piece.</returns>
    private static TranscriptSegment Piece(string text)
    {
        return new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), text);
    }
}
