namespace Cardscape.Seeder.Reporting;

/// <summary>One line of seeder output, surfaced to the live UI
/// through the polling endpoint and to the file sink.</summary>
public sealed record SeedLogEntry(
    DateTimeOffset At,
    SeedLogLevel Level,
    string Step,
    string Message)
{
    public string LevelCssClass => Level switch
    {
        SeedLogLevel.Success => "rz-color-success",
        SeedLogLevel.Warning => "rz-color-warning",
        SeedLogLevel.Error => "rz-color-danger",
        _ => "rz-color-text-secondary"
    };

    public string LevelBadge => Level switch
    {
        SeedLogLevel.Success => "OK",
        SeedLogLevel.Warning => "WARN",
        SeedLogLevel.Error => "FAIL",
        _ => "INFO"
    };
}
