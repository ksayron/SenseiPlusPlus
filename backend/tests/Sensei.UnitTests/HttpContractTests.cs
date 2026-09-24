using Sensei.BuildingBlocks.Api;

namespace Sensei.UnitTests;

public sealed class HttpContractTests
{
    [Fact]
    public void IfMatch_requires_one_strong_current_version_token()
    {
        var token = HttpContract.VersionToken(3);
        Assert.Equal(3, HttpContract.RequireVersion($"\"{token}\""));

        Assert.Equal(428, Assert.Throws<ApiContractException>(() => HttpContract.RequireVersion((string?)null)).StatusCode);
        foreach (var malformed in new[] { "*", $"W/\"{token}\"", $"\"{token}\", \"{token}\"", "\"bad\"" })
        {
            var error = Assert.Throws<ApiContractException>(() => HttpContract.RequireVersion(malformed));
            Assert.Equal(400, error.StatusCode);
            Assert.Equal("validation_failed", error.Code);
        }
    }

    [Fact]
    public void Cursor_paginates_and_cannot_be_reused_for_another_scope()
    {
        var items = Enumerable.Range(1, 3).Select(number => new Item(Guid.NewGuid(), number)).ToArray();
        var first = HttpContract.Page(items, 2, null, "owner-a:all", item => item.Number.ToString(), item => item.Id);
        Assert.Equal([1, 2], first.Items.Select(item => item.Number));
        Assert.NotNull(first.NextCursor);

        var second = HttpContract.Page(items, 2, first.NextCursor, "owner-a:all", item => item.Number.ToString(), item => item.Id);
        Assert.Equal([3], second.Items.Select(item => item.Number));
        Assert.Null(second.NextCursor);

        var error = Assert.Throws<ApiContractException>(() =>
            HttpContract.Page(items, 2, first.NextCursor, "owner-b:all", item => item.Number.ToString(), item => item.Id));
        Assert.Equal("invalid_cursor", error.Code);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("invalid_cursor", Assert.Throws<ApiContractException>(() =>
            HttpContract.Page(items, 2, "", "owner-a:all", item => item.Number.ToString(), item => item.Id)).Code);
    }

    private sealed record Item(Guid Id, int Number);
}
