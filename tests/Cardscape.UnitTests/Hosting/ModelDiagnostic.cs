// Temporary diagnostic scaffolding used to confirm the
// EF Core 10 SQLite DateTimeOffset translation limitation
// (documented in Cardscape.Infrastructure/Repositories/RevokedTokenRepository.cs
// lines 40-55). Kept as a skipped class so the test
// assembly still compiles. Re-enable locally by setting
// `Skip = null` if you need to re-investigate the model.
namespace Cardscape.UnitTests.Hosting;

public class ModelDiagnostic
{
    [Fact(Skip = "diagnostic only — see file header")]
    public void TranslateRetentionPredicate()
    {
        // intentionally empty
    }
}
