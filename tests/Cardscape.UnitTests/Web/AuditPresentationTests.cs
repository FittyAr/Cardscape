using System.Xml.Linq;
using Cardscape.Domain.Audit;
using Cardscape.Web.Shared;

namespace Cardscape.UnitTests.Web;

public sealed class AuditPresentationTests
{
    [Theory]
    [InlineData("workspace.member_role_changed", "AuditWorkspaceMemberRoleChanged")]
    [InlineData("user.admin_granted", "AuditUserAdminGranted")]
    [InlineData("board.member_added", "AuditBoardMemberAdded")]
    public void SentenceKey_IsThePascalCaseActionCode(string action, string key) =>
        AuditPresentation.SentenceKey(action).Should().Be(key);

    [Theory]
    [InlineData("SharedResource.resx")]
    [InlineData("SharedResource.es.resx")]
    public void EveryActionCode_HasASentence_InEveryLanguage(string file)
    {
        string path = Path.Combine(RepositoryRoot(), "src", "Cardscape.Web", "Resources", file);
        HashSet<string> keys = XDocument.Load(path).Root!.Elements("data")
            .Select(data => (string)data.Attribute("name")!)
            .ToHashSet(StringComparer.Ordinal);

        AuditActions.All.Select(AuditPresentation.SentenceKey).Should().OnlyContain(key => keys.Contains(key));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cardscape.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
