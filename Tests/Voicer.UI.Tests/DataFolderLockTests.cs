using FluentAssertions;
using Voicer.UI.Services.Lifetime;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the lock of the data folder: one window holds it at a time.
/// </summary>
public sealed class DataFolderLockTests : IDisposable
{
    /// <summary>
    /// A temporary data folder.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-lock-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// The first window holds the folder, a second one does not; once the first lets it go, the next one holds it.
    /// </summary>
    [Fact]
    public void OneWindow_HoldsTheFolder()
    {
        using (var first = DataFolderLock.Acquire(_folder))
        {
            first.IsHeld.Should().BeTrue();

            using var second = DataFolderLock.Acquire(_folder);
            second.IsHeld.Should().BeFalse();
        }

        using var next = DataFolderLock.Acquire(_folder);
        next.IsHeld.Should().BeTrue();
    }
}
