using FluentAssertions;
using MapleClaude.Domain;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

public sealed class PortalVisualTests
{
    [Theory]
    [InlineData(2, "pv")]
    public void Visible_portal_types_resolve_to_expected_MapHelper_animation(int portalType, string expectedPath)
    {
        var visual = PortalVisual.ForType(portalType);

        visual.Should().NotBeNull();
        visual!.AnimationPath.Should().Be(expectedPath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(999)]
    public void Spawn_invisible_and_unknown_portal_types_do_not_draw(int portalType)
    {
        PortalVisual.ForType(portalType).Should().BeNull();
    }
}
