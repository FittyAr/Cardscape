using System.Text;
using Cardscape.Infrastructure.BackgroundJobs;

namespace Cardscape.UnitTests.Infrastructure.BackgroundJobs;

public sealed class WebhookDeliveryHandlerTests
{
    [Fact]
    public void SignBody_UsesCleartextSecretAsHmacKey()
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"event\":\"card.created\"}");

        string signature = WebhookDeliveryHandler.SignBody("super-secret", body);

        signature.Should().Be(
            "sha256=579c9739fdf6d6ac391421b3ad5331161df10996f8c2acc5b3b734b3e2d67f11");
    }
}
