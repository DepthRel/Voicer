using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Voicer.Conventions.Tests;

/// <summary>
/// Tests of the project rules of AGENTS.md beyond code style: where time comes from
/// and where projects sit in the solution.
/// </summary>
public sealed class ProjectRulesTests
{
    /// <summary>
    /// The members that read the clock of the machine directly, as type and member names.
    /// </summary>
    private static readonly HashSet<(string Type, string Member)> _clockReads =
    [
        ("DateTime", "Now"),
        ("DateTime", "UtcNow"),
        ("DateTime", "Today"),
        ("DateTimeOffset", "Now"),
        ("DateTimeOffset", "UtcNow"),
        ("Stopwatch", "StartNew"),
        ("Stopwatch", "GetTimestamp"),
        ("Environment", "TickCount"),
        ("Environment", "TickCount64"),
    ];


    /// <summary>
    /// Time comes from <see cref="TimeProvider"/>: the code of the product never reads the
    /// clock of the machine directly, so tests can control it.
    /// </summary>
    [Fact]
    public void Time_ComesFromTimeProvider()
    {
        var reads = RepositorySources.Products
            .SelectMany(f => f.Root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(a => _clockReads.Contains((OwnerOf(a), a.Name.Identifier.Text)))
                .Select(a => f.At(a)));
        var stopwatches = RepositorySources.Products
            .SelectMany(f => f.Root.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(c => c.Type.ToString().EndsWith("Stopwatch", StringComparison.Ordinal))
                .Select(c => f.At(c)));

        ConventionAssert.None(reads.Concat(stopwatches), "A direct read of the clock instead of TimeProvider:");
    }

    /// <summary>
    /// Every project is in Voicer.slnx, in the solution folder that repeats its path on disk.
    /// </summary>
    [Fact]
    public void Projects_SitInTheSolutionFolderOfTheirPath()
    {
        var solution = XDocument.Load(Path.Combine(RepositorySources.Root, "Voicer.slnx"));
        var folders = solution.Descendants("Project")
            .ToDictionary(
                p => (string)p.Attribute("Path")!,
                p => (string?)p.Parent?.Attribute("Name"),
                StringComparer.OrdinalIgnoreCase);

        var findings = RepositorySources.Files("Sources", "*.csproj")
            .Concat(RepositorySources.Files("Tests", "*.csproj"))
            .Select(p => RepositorySources.Relative(p).Replace('\\', '/'))
            .Where(p => !folders.TryGetValue(p, out var folder) ||
                folder != ExpectedFolder(p));

        ConventionAssert.None(findings, "A project missing from Voicer.slnx or in another solution folder:");
    }

    /// <summary>
    /// Gives the name of the type or object a member is read from: the last identifier before the dot.
    /// </summary>
    /// <param name="access">The member access.</param>
    /// <returns>The name, or an empty string when it is not a plain name.</returns>
    private static string OwnerOf(MemberAccessExpressionSyntax access)
    {
        return access.Expression switch
        {
            IdentifierNameSyntax name => name.Identifier.Text,
            MemberAccessExpressionSyntax inner => inner.Name.Identifier.Text,
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Gives the solution folder a project belongs to: the folder that holds the folder of the project.
    /// </summary>
    /// <param name="project">The path of the project file relative to the root, with forward slashes.</param>
    /// <returns>The folder name as Voicer.slnx writes it, such as <c>/Sources/Apps/</c>.</returns>
    private static string ExpectedFolder(string project)
    {
        var parts = project.Split('/');

        return "/" + string.Join('/', parts[..^2]) + "/";
    }
}
