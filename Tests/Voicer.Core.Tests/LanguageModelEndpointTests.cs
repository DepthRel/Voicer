using FluentAssertions;
using Voicer.Core.Summaries;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the address of a server of language models typed by the user.
/// </summary>
public sealed class LanguageModelEndpointTests
{
    /// <summary>
    /// An http or https address is taken without its trailing slash; the methods are under it.
    /// </summary>
    [Fact]
    public void TryCreate_TakesAnHttpAddress()
    {
        LanguageModelEndpoint.TryCreate(" http://localhost:1234/v1/ ", "  ", out var endpoint).Should().BeTrue();

        endpoint.ApiKey.Should().BeNull();
        endpoint.Resolve("models").AbsoluteUri.Should().Be("http://localhost:1234/v1/models");
        endpoint.Resolve("/chat/completions").AbsoluteUri.Should().Be("http://localhost:1234/v1/chat/completions");
    }

    /// <summary>
    /// Addresses that are not the address of an API are refused.
    /// </summary>
    /// <param name="address">The address.</param>
    [Theory]
    [InlineData("")]
    [InlineData("localhost:1234/v1")]
    [InlineData("ftp://server/v1")]
    [InlineData("file:///C:/models")]
    [InlineData("http://localhost:1234/v1?key=1")]
    public void TryCreate_RefusesOtherAddresses(string address)
    {
        LanguageModelEndpoint.TryCreate(address, null, out _).Should().BeFalse();
    }
}
