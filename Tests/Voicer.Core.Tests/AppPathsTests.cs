using FluentAssertions;
using Voicer.Core.Paths;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the choice of the data folder.
/// </summary>
public sealed class AppPathsTests : IDisposable
{
    /// <summary>
    /// A temporary folder that plays the folder of the application.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "voicer-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The profile folder given to the resolver.
    /// </summary>
    private readonly string _profile = Path.Combine(Path.GetTempPath(), "voicer-profile");

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// <c>--data</c> names the data folder whatever else is there.
    /// </summary>
    [Fact]
    public void Resolve_TakesTheDataOption()
    {
        var explicitFolder = Path.Combine(_root, "custom");

        var paths = AppPaths.Resolve(_root, ["--data", explicitFolder], _ => true, _profile);

        paths.DataDirectory.Should().Be(Path.GetFullPath(explicitFolder));
    }

    /// <summary>
    /// A program inside a package (the marker is in the folder above it) keeps its data at the root of the package.
    /// </summary>
    [Fact]
    public void Resolve_PutsDataNextToThePortableMarker()
    {
        var program = Path.Combine(_root, "ui");
        Directory.CreateDirectory(program);
        File.WriteAllText(Path.Combine(_root, AppPaths.PORTABLE_MARKER), string.Empty);

        var paths = AppPaths.Resolve(program, [], _ => true, _profile);

        paths.DataDirectory.Should().Be(Path.Combine(_root, AppPaths.DATA_FOLDER));
        paths.ModelsDirectory.Should().Be(Path.Combine(_root, "Data", "Models"));
    }

    /// <summary>
    /// Without a marker the data folder sits next to the program.
    /// </summary>
    [Fact]
    public void Resolve_PutsDataNextToTheProgramWithoutMarker()
    {
        var paths = AppPaths.Resolve(_root, [], _ => true, _profile);

        paths.DataDirectory.Should().Be(Path.Combine(_root, AppPaths.DATA_FOLDER));
    }

    /// <summary>
    /// A folder the user cannot write to sends the data to the profile.
    /// </summary>
    [Fact]
    public void Resolve_FallsBackToTheProfile()
    {
        var paths = AppPaths.Resolve(_root, [], _ => false, _profile);

        paths.DataDirectory.Should().Be(Path.Combine(_profile, "Voicer", "Data"));
    }

    /// <summary>
    /// The probe of a folder creates it and tells it is writable.
    /// </summary>
    [Fact]
    public void CanWrite_CreatesAWritableFolder()
    {
        var folder = Path.Combine(_root, "probe");

        AppPaths.CanWrite(folder).Should().BeTrue();
        Directory.Exists(folder).Should().BeTrue();
        Directory.EnumerateFileSystemEntries(folder).Should().BeEmpty();
    }
}
