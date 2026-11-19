using FluentAssertions;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the parts of the search: the passages, the full-text query, the fusion of the lists, the prefixes of the
/// models of embeddings and the arithmetic of the vectors.
/// </summary>
public sealed class SearchPartsTests
{
    /// <summary>
    /// Fragments are put together until a passage is long enough; every passage keeps its moments.
    /// </summary>
    [Fact]
    public void Passages_GatherFragments()
    {
        var long1 = new string('а', PassageBuilder.TARGET_LENGTH - 5);
        TranscriptSegment[] segments =
        [
            new(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), long1),
            new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(9), "  второй.  "),
            new(TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(10), " "),
            new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12), "третий."),
        ];

        var passages = PassageBuilder.Build(segments);

        passages.Should().Equal(
            new Passage(TimeSpan.Zero, TimeSpan.FromSeconds(9), long1 + " второй."),
            new Passage(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12), "третий."));
    }

    /// <summary>
    /// Words are looked up by their beginning, phrases in quotes as they are, anything else is left out.
    /// </summary>
    [Fact]
    public void FullTextQuery_BuildsSafeTerms()
    {
        FullTextQuery.Build("Договоров  бюджет").Should().Be("\"договор\"* OR \"бюдже\"*");
        FullTextQuery.Build("\"Итоги Встречи\" план").Should().Be("\"итоги встречи\" OR \"план\"*");
        FullTextQuery.Build("NEAR(a b) OR x* -y \"").Should().Be("\"near\"* OR \"a\"* OR \"b\"* OR \"or\"* OR \"x\"* OR \"y\"*");
        FullTextQuery.Build("  ?!  ").Should().BeNull();
        FullTextQuery.Build(null).Should().BeNull();
    }

    /// <summary>
    /// A passage both lists like comes first; the others follow by their ranks.
    /// </summary>
    [Fact]
    public void Fusion_PrefersWhatBothListsFound()
    {
        var a = Hit(1);
        var b = Hit(2);
        var c = Hit(3);

        var fused = RankFusion.Fuse([[a, b], [c, b]], 10);

        fused.Should().Equal(b, a, c);
        RankFusion.Fuse([[a, b], [c, b]], 1).Should().Equal(b);
    }

    /// <summary>
    /// The families that need prefixes get them; the others take the text as it is.
    /// </summary>
    [Fact]
    public void EmbeddingText_KnowsThePrefixes()
    {
        EmbeddingText.Query("intfloat/multilingual-e5-large", "бюджет").Should().Be("query: бюджет");
        EmbeddingText.Passage("multilingual-e5-small", "текст").Should().Be("passage: текст");
        EmbeddingText.Query("text-embedding-nomic-embed-text-v1.5", "q").Should().Be("search_query: q");
        EmbeddingText.Passage("bge-m3", "текст").Should().Be("текст");
        EmbeddingText.Query("qwen2.5-coder-7b", "q").Should().Be("q");
    }

    /// <summary>
    /// A normalized vector has unit length; similar vectors score higher; vectors of different lengths never match.
    /// </summary>
    [Fact]
    public void Vectors_AreNormalizedAndCompared()
    {
        var x = VectorMath.Normalize([3, 4]);

        x.Should().Equal(0.6f, 0.8f);
        VectorMath.Similarity(x, x).Should().BeApproximately(1, 1e-6f);
        VectorMath.Similarity(x, VectorMath.Normalize([4, -3])).Should().BeApproximately(0, 1e-6f);
        VectorMath.Similarity(x, [1, 0, 0]).Should().Be(float.NegativeInfinity);
        VectorMath.Normalize([0, 0]).Should().Equal(0, 0);
    }

    /// <summary>
    /// Creates a passage of a test.
    /// </summary>
    /// <param name="id">The identifier of the passage.</param>
    /// <returns>The passage.</returns>
    private static PassageHit Hit(long id)
    {
        return new PassageHit(id, 1, "talk.mp3", DateTimeOffset.UnixEpoch, TimeSpan.Zero, TimeSpan.FromSeconds(1), "text " + id);
    }
}
