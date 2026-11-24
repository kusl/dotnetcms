using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class ReaderTrackingServiceTests
{
    private readonly ReaderTrackingService _sut = new();

    [Fact]
    public void Track_WithNewSlug_CountsOneReader()
    {
        using var reader = _sut.Track("test-slug", () => { });

        Assert.Equal(1, _sut.GetReaderCount("test-slug"));
    }

    [Fact]
    public void Track_WithMultipleReaders_CountsEachReader()
    {
        using var reader1 = _sut.Track("test-slug", () => { });
        using var reader2 = _sut.Track("test-slug", () => { });
        using var reader3 = _sut.Track("test-slug", () => { });

        Assert.Equal(3, _sut.GetReaderCount("test-slug"));
    }

    [Fact]
    public void Track_WithDifferentSlugs_CountsIndependently()
    {
        using var reader1 = _sut.Track("slug-a", () => { });
        using var reader2 = _sut.Track("slug-b", () => { });
        using var reader3 = _sut.Track("slug-a", () => { });

        Assert.Equal(2, _sut.GetReaderCount("slug-a"));
        Assert.Equal(1, _sut.GetReaderCount("slug-b"));
    }

    [Fact]
    public void Track_SameCallbackTwice_CountsTwoReaders()
    {
        Action callback = () => { };

        using var reader1 = _sut.Track("test-slug", callback);
        using var reader2 = _sut.Track("test-slug", callback);

        Assert.Equal(2, _sut.GetReaderCount("test-slug"));
    }

    [Fact]
    public void Dispose_RemovesOnlyThatReader()
    {
        using var reader1 = _sut.Track("test-slug", () => { });
        var reader2 = _sut.Track("test-slug", () => { });

        reader2.Dispose();

        Assert.Equal(1, _sut.GetReaderCount("test-slug"));
    }

    [Fact]
    public void Dispose_CalledTwice_RemovesReaderOnce()
    {
        using var reader1 = _sut.Track("test-slug", () => { });
        var reader2 = _sut.Track("test-slug", () => { });

        reader2.Dispose();
        reader2.Dispose();

        Assert.Equal(1, _sut.GetReaderCount("test-slug"));
    }

    [Fact]
    public void Dispose_LastReader_ReturnsZero()
    {
        var reader = _sut.Track("test-slug", () => { });

        reader.Dispose();

        Assert.Equal(0, _sut.GetReaderCount("test-slug"));
    }

    [Fact]
    public void GetReaderCount_WithUnknownSlug_ReturnsZero()
    {
        Assert.Equal(0, _sut.GetReaderCount("non-existent"));
    }

    [Fact]
    public void Track_NotifiesExistingAndNewReadersOfSameSlug()
    {
        var firstNotifications = 0;
        var secondNotifications = 0;

        using var reader1 = _sut.Track("test-slug", () => firstNotifications++);
        using var reader2 = _sut.Track("test-slug", () => secondNotifications++);

        Assert.Equal(2, firstNotifications);
        Assert.Equal(1, secondNotifications);
    }

    [Fact]
    public void Track_DoesNotNotifyReadersOfOtherSlugs()
    {
        var notifications = 0;

        using var reader1 = _sut.Track("slug-a", () => notifications++);
        using var reader2 = _sut.Track("slug-b", () => { });

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void Dispose_NotifiesRemainingReadersButNotLeavingReader()
    {
        var remainingNotifications = 0;
        var leavingNotifications = 0;

        using var remaining = _sut.Track("test-slug", () => remainingNotifications++);
        var leaving = _sut.Track("test-slug", () => leavingNotifications++);
        remainingNotifications = 0;
        leavingNotifications = 0;

        leaving.Dispose();

        Assert.Equal(1, remainingNotifications);
        Assert.Equal(0, leavingNotifications);
    }

    [Fact]
    public void Notification_SeesUpdatedCount()
    {
        var observed = new List<int>();

        using var reader1 = _sut.Track("test-slug", () => observed.Add(_sut.GetReaderCount("test-slug")));
        var reader2 = _sut.Track("test-slug", () => { });
        reader2.Dispose();

        Assert.Equal(new[] { 1, 2, 1 }, observed);
    }

    [Fact]
    public void Track_WithEmptySlug_Throws()
    {
        Assert.Throws<ArgumentException>("slug", () => _sut.Track("", () => { }));
    }

    [Fact]
    public void Track_WithNullCallback_Throws()
    {
        Assert.Throws<ArgumentNullException>("onReadersChanged", () => _sut.Track("test-slug", null!));
    }

    [Fact]
    public async Task ConcurrentTracks_MaintainAccurateCount()
    {
        var ct = TestContext.Current.CancellationToken;
        const int readerCount = 100;
        var tasks = new List<Task<IDisposable>>();

        for (var i = 0; i < readerCount; i++)
        {
            tasks.Add(Task.Run(() => _sut.Track("test-slug", () => { }), ct));
        }

        var readers = await Task.WhenAll(tasks);

        Assert.Equal(readerCount, _sut.GetReaderCount("test-slug"));

        foreach (var reader in readers)
        {
            reader.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentTracksAndDisposals_MaintainAccurateCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var initial = new List<IDisposable>();

        for (var i = 0; i < 50; i++)
        {
            initial.Add(_sut.Track("test-slug", () => { }));
        }

        var tasks = new List<Task>();

        for (var i = 0; i < 25; i++)
        {
            tasks.Add(Task.Run(() => _sut.Track("test-slug", () => { }), ct));
        }

        for (var i = 0; i < 25; i++)
        {
            var reader = initial[i];
            tasks.Add(Task.Run(() => reader.Dispose(), ct));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(50, _sut.GetReaderCount("test-slug"));
    }

    [Fact]
    public async Task ConcurrentDisposals_ReturnCountToZero()
    {
        var ct = TestContext.Current.CancellationToken;
        const int readerCount = 50;
        var readers = new List<IDisposable>();

        for (var i = 0; i < readerCount; i++)
        {
            readers.Add(_sut.Track("test-slug", () => { }));
        }

        var tasks = readers.Select(reader => Task.Run(() => reader.Dispose(), ct)).ToList();

        await Task.WhenAll(tasks);

        Assert.Equal(0, _sut.GetReaderCount("test-slug"));
    }
}
