using FluentAssertions;
using Voicer.UI.Services.Diagnostics;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the log of the unexpected failures of the window.
/// </summary>
public sealed class ErrorLogTests : IDisposable
{
    /// <summary>
    /// A folder of its own for every test.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-log-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Every failure is added with its moment, type and message; the folder is created when it is missing.
    /// </summary>
    [Fact]
    public void Write_AddsEveryFailure()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 19, 17, 5, 0, TimeSpan.FromHours(3)));
        var log = new ErrorLog(Path.Combine(_folder, "errors.log"), clock);

        log.Write(new InvalidOperationException("first")).Should().BeTrue();
        log.Write(new IOException("second")).Should().BeTrue();

        var text = File.ReadAllText(log.FilePath);
        text.Should().Contain("2026-11-19 17:05:00 +03:00")
            .And.Contain("System.InvalidOperationException: first")
            .And.Contain("System.IO.IOException: second");
    }

    /// <summary>
    /// A log that cannot be written says so instead of failing in its turn.
    /// </summary>
    [Fact]
    public void Write_WhenTheFileCannotBeWritten_ReturnsFalse()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "errors.log"));
        var log = new ErrorLog(Path.Combine(_folder, "errors.log"));

        log.Write(new InvalidOperationException("lost")).Should().BeFalse();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// A clock that always tells the same moment, in its own time zone.
    /// </summary>
    /// <param name="now">The moment.</param>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        /// <inheritdoc />
        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("test", now.Offset, "test", "test");
    }
}
