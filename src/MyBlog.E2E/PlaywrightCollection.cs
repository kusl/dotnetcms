using Xunit;

namespace MyBlog.E2E;

/// <summary>
/// Collection definition for sharing PlaywrightFixture across tests.
/// All tests using this collection share a single browser instance.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Playwright";
}
