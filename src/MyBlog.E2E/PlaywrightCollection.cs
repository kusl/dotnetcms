using Xunit;

namespace MyBlog.E2E;

[CollectionDefinition(Name)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Playwright";
}
