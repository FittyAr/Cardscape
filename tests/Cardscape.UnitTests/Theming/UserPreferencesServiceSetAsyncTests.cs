// UserPreferencesServiceSetAsyncTests — pins down the
// 404-retry behaviour added by R10-UI-#1. The bug was
// that a fresh user (no user_preferences row yet) who
// clicks "Apply" on a theme card saw a client-side
// theme change but no server-side persistence: the
// PUT /api/users/me/preferences endpoint returns 404
// with code `members.user_preferences.not_found` for
// the no-row case, and SetAsync was logging the
// failure and moving on.
//
// The fix detects the 404, calls CreateDefaultAsync
// to bootstrap the row with project defaults, then
// retries the PUT so the user's actual choice is
// what lands in the DB. These tests cover the three
// observable outcomes:
//
//  1. Fresh user, PUT returns 404, POST + retry PUT
//     succeed → SetAsync completes without warning.
//  2. Existing user, PUT returns 200 → SetAsync skips
//     CreateDefaultAsync entirely (the bug was that
//     we never got here; this is the happy path).
//  3. Fresh user, PUT 404 + POST 500 → SetAsync logs
//     a warning and does not throw (the user still
//     gets the cookie write, just no server
//     persistence).

using System.Security.Claims;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radzen;
using ThemeServiceAlias = Radzen.ThemeService;

namespace Cardscape.UnitTests.Theming;

public class UserPreferencesServiceSetAsyncTests
{
    private static readonly Guid AnyUserId = new("92d0ee1e-2778-4eb0-b741-59c94b08f071");

    private static UserPreferencesDto MakeDto(string theme = "default", string mode = "System") =>
        new(AnyUserId, theme, mode, DateTimeOffset.UtcNow, null);

    /// <summary>A real <see cref="ThemeService"/> wired to
    /// a null <c>IJSRuntime</c> and an empty service
    /// provider. SetTheme() will throw inside the
    /// service, but the catch block in
    /// <c>UserPreferencesService.ApplyThemeName</c>
    /// swallows it (the cookie write is a write-through
    /// cache, not the source of truth). We only care
    /// about the SetAsync post-cookie behaviour here.</summary>
    private static ThemeServiceAlias NewThemeService() =>
        new(null!, new EmptyServiceProvider());

    private static UserPreferencesService NewService(IUserPreferencesApiClient api) =>
        new(
            api: api,
            themeService: NewThemeService(),
            auth: new StubAuthProvider(AnyUserId),
            log: NullLogger<UserPreferencesService>.Instance);

    [Fact]
    public async Task SetAsync_WhenPutReturns404_CallsCreateDefaultAndRetriesPut()
    {
        // R10-UI-#1 main scenario: fresh user clicks
        // "Apply" before the GET-then-POST bootstrap has
        // happened. PUT 404 → POST defaults → PUT retry.
        var api = new Mock<IUserPreferencesApiClient>();
        int updateCalls = 0;
        api.Setup(a => a.UpdateAsync("default", "Light", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++updateCalls == 1
                ? ApiResult<UserPreferencesDto>.Fail("not_found", statusCode: 404)
                : ApiResult<UserPreferencesDto>.Ok(MakeDto("default", "Light")));
        api.Setup(a => a.CreateDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserPreferencesDto>.Ok(MakeDto("default", "System")));

        var sut = NewService(api.Object);

        // Should NOT throw. The local state should be
        // applied (cookie write attempted) and the
        // server should have the user's choice
        // persisted (PUT retry succeeded).
        await sut.SetAsync("default", "Light");

        api.Verify(
            a => a.UpdateAsync("default", "Light", It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        api.Verify(
            a => a.CreateDefaultAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_WhenPutSucceeds_DoesNotCallCreateDefault()
    {
        // Happy path: existing user with a row. The
        // 404-retry branch must NOT fire — that would
        // mean a pointless POST on every theme change
        // for already-bootstrapped users.
        var api = new Mock<IUserPreferencesApiClient>();
        api.Setup(a => a.UpdateAsync("software", "Dark", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserPreferencesDto>.Ok(MakeDto("software", "Dark")));

        var sut = NewService(api.Object);

        await sut.SetAsync("software", "Dark");

        api.Verify(
            a => a.UpdateAsync("software", "Dark", It.IsAny<CancellationToken>()),
            Times.Once);
        api.Verify(
            a => a.CreateDefaultAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetAsync_WhenPutReturns500_DoesNotCallCreateDefault()
    {
        // Different failure mode (server 5xx). The fix
        // is 404-specific: only the not_found code
        // means "no row, please create one". A 500
        // means something else is wrong; do not try to
        // create a row on top of a real error.
        var api = new Mock<IUserPreferencesApiClient>();
        api.Setup(a => a.UpdateAsync("default", "Light", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserPreferencesDto>.Fail("server_error", statusCode: 500));

        var sut = NewService(api.Object);

        // Should NOT throw. The local cookie write
        // still happens; the server-side save is best
        // effort and just gets logged.
        await sut.SetAsync("default", "Light");

        api.Verify(
            a => a.CreateDefaultAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetAsync_WhenCreateDefaultFails_DoesNotRetryAndDoesNotThrow()
    {
        // 404 on PUT, but POST also fails (e.g. the
        // user is racing with a soft-delete that
        // already removed them). The fix must not
        // throw — the user still got their cookie
        // change, the server just rejected both
        // calls. We log and move on.
        var api = new Mock<IUserPreferencesApiClient>();
        api.Setup(a => a.UpdateAsync("default", "Light", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserPreferencesDto>.Fail("not_found", statusCode: 404));
        api.Setup(a => a.CreateDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserPreferencesDto>.Fail("server_error", statusCode: 500));

        var sut = NewService(api.Object);

        // The retry PUT is conditional on create
        // success, so the second UpdateAsync must NOT
        // fire here.
        await sut.SetAsync("default", "Light");

        api.Verify(
            a => a.UpdateAsync("default", "Light", It.IsAny<CancellationToken>()),
            Times.Once);
        api.Verify(
            a => a.CreateDefaultAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithUnknownThemeName_DoesNotCallApi()
    {
        // Defensive: a typo'd theme (e.g. a stale
        // cookie that survived a catalog rename) is
        // caught by the catalog whitelist before any
        // HTTP call. The fix does not change this
        // contract; pinned here to keep it that way.
        var api = new Mock<IUserPreferencesApiClient>(MockBehavior.Strict);

        var sut = NewService(api.Object);

        await sut.SetAsync("not-a-real-theme", "Light");

        api.VerifyNoOtherCalls();
    }

    /// <summary>Minimum-viable
    /// <see cref="AuthenticationStateProvider"/> that
    /// reports the test user as authenticated. The
    /// service only checks for "is the user signed in
    /// at all" before talking to the server; this
    /// stub is enough for the SetAsync paths above.</summary>
    private sealed class StubAuthProvider : AuthenticationStateProvider
    {
        private readonly Task<AuthenticationState> _state;

        public StubAuthProvider(Guid userId)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                authenticationType: "test");
            _state = Task.FromResult(new AuthenticationState(
                new ClaimsPrincipal(identity)));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state;
    }

    /// <summary>Empty <see cref="IServiceProvider"/> used
    /// to construct Radzen's <see cref="ThemeService"/>
    /// without bringing in a real DI container. The
    /// <c>ThemeService</c> resolves a few services on
    /// first use, all of which gracefully no-op when
    /// absent in tests.</summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
