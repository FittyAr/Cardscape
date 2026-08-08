// UserPreferencesTests — in-process unit tests for the
// UserPreferences aggregate. The aggregate is small (3
// public fields, 1 create, 1 update) but the contract
// matters: the domain is the validation gate for the
// theme name + mode pair that the Blazor client
// persists via ThemeService.SetTheme. A regression here
// would let a typo'd theme name break the runtime CSS
// resolution, so the rules are pinned down explicitly.

using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using Cardscape.Domain.UserPreferences.Errors;
using Cardscape.Domain.UserPreferences.Events;
using FluentAssertions;
using UserPreferencesAggregate = Cardscape.Domain.UserPreferences.UserPreferences;

namespace Cardscape.UnitTests.UserPreferences;

public class UserPreferencesTests
{
    private static readonly UserId AnyUser = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyCollection<string> AllValidNames = new[]
    {
        "default", "dark",
        "humanistic", "humanistic-dark",
        "material", "material-dark",
        "software", "software-dark",
        "standard", "standard-dark",
        "cardscape-classic", "cardscape-classic-dark",
    };

    [Fact]
    public void Create_WithDefaults_StoresDefaultsAndRaisesCreatedEvent()
    {
        var result = UserPreferencesAggregate.Create(
            userId: AnyUser,
            themeName: UserPreferencesAggregate.DefaultThemeName,
            mode: AppearanceMode.System,
            at: Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(AnyUser);
        result.Value.ThemeName.Should().Be("default");
        result.Value.Mode.Should().Be(AppearanceMode.System);
        result.Value.CreatedAt.Should().Be(Now);

        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserPreferencesCreated>()
            .Which.OccurredAt.Should().Be(Now);
    }

    [Fact]
    public void Create_WithNullUserId_Fails()
    {
        var result = UserPreferencesAggregate.Create(
            userId: null!,
            themeName: "default",
            mode: AppearanceMode.System,
            at: Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithEmptyThemeName_Fails()
    {
        var result = UserPreferencesAggregate.Create(
            userId: AnyUser,
            themeName: "   ",
            mode: AppearanceMode.System,
            at: Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Update_ChangesThemeAndStampsChanged()
    {
        var prefs = UserPreferencesAggregate.Create(AnyUser, "default", AppearanceMode.System, Now).Value;
        prefs.ClearDomainEvents();

        var update = prefs.Update(
            themeName: "cardscape-classic",
            mode: null,
            validThemeNames: AllValidNames,
            at: Now.AddMinutes(1));

        update.IsSuccess.Should().BeTrue();
        prefs.ThemeName.Should().Be("cardscape-classic");
        prefs.Mode.Should().Be(AppearanceMode.System);
        prefs.UpdatedAt.Should().Be(Now.AddMinutes(1));

        prefs.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserPreferencesUpdated>();
    }

    [Fact]
    public void Update_WithUnknownThemeName_FailsAndDoesNotMutate()
    {
        var prefs = UserPreferencesAggregate.Create(AnyUser, "default", AppearanceMode.System, Now).Value;
        prefs.ClearDomainEvents();

        var update = prefs.Update(
            themeName: "not-a-real-theme",
            mode: null,
            validThemeNames: AllValidNames,
            at: Now.AddMinutes(1));

        update.IsFailure.Should().BeTrue();
        update.Error.Should().Be(UserPreferencesErrors.InvalidThemeName);

        // The aggregate must not be half-mutated on a
        // validation failure.
        prefs.ThemeName.Should().Be("default");
        prefs.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithSameValues_IsNoOp()
    {
        var prefs = UserPreferencesAggregate.Create(AnyUser, "default", AppearanceMode.System, Now).Value;
        prefs.ClearDomainEvents();

        var update = prefs.Update(
            themeName: "default",
            mode: AppearanceMode.System,
            validThemeNames: AllValidNames,
            at: Now.AddMinutes(1));

        update.IsSuccess.Should().BeTrue();
        // No mutation means no event and no UpdatedAt bump.
        prefs.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData((AppearanceMode)99)]
    [InlineData((AppearanceMode)(-1))]
    public void Update_WithUndefinedMode_Fails(AppearanceMode badMode)
    {
        var prefs = UserPreferencesAggregate.Create(AnyUser, "default", AppearanceMode.System, Now).Value;
        var update = prefs.Update(themeName: null, mode: badMode, validThemeNames: AllValidNames, at: Now);

        update.IsFailure.Should().BeTrue();
        update.Error.Should().Be(UserPreferencesErrors.InvalidMode);
    }

    [Fact]
    public void Update_TrimsWhitespaceInThemeName()
    {
        var prefs = UserPreferencesAggregate.Create(AnyUser, "default", AppearanceMode.System, Now).Value;

        var update = prefs.Update(
            themeName: "  software  ",
            mode: null,
            validThemeNames: AllValidNames,
            at: Now);

        update.IsSuccess.Should().BeTrue();
        prefs.ThemeName.Should().Be("software");
    }
}

