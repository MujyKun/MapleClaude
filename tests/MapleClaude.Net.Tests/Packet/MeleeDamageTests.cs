using FluentAssertions;
using MapleClaude.Net.Packet;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

/// <summary>
/// Phase 21 combat-depth: the client-side melee damage estimate. The v95 server
/// trusts client damage, so these assert the estimate is well-formed and scales
/// sensibly with the character — not byte-accuracy (which needs weapon/mastery
/// data that isn't wired yet).
/// </summary>
public class MeleeDamageTests
{
    [Fact]
    public void Estimate_MinNotGreaterThanMax_AndBothPositive()
    {
        var (min, max) = MeleeDamage.Estimate(jobId: 100, level: 10, str: 35, dex: 10, @int: 4, luk: 4);
        min.Should().BeGreaterThanOrEqualTo(1);
        max.Should().BeGreaterThanOrEqualTo(min);
    }

    [Fact]
    public void Estimate_NeverZero_ForFreshBeginner()
    {
        var (min, max) = MeleeDamage.Estimate(jobId: 0, level: 1, str: 4, dex: 4, @int: 4, luk: 4);
        min.Should().BeGreaterThanOrEqualTo(1);
        max.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Estimate_ScalesWithPrimaryStat()
    {
        var (_, weakMax) = MeleeDamage.Estimate(100, 30, str: 40, dex: 10, @int: 4, luk: 4);
        var (_, strongMax) = MeleeDamage.Estimate(100, 30, str: 200, dex: 10, @int: 4, luk: 4);
        strongMax.Should().BeGreaterThan(weakMax);
    }

    [Fact]
    public void Estimate_ScalesWithLevel()
    {
        var (_, lowMax) = MeleeDamage.Estimate(100, level: 5, str: 50, dex: 10, @int: 4, luk: 4);
        var (_, highMax) = MeleeDamage.Estimate(100, level: 80, str: 50, dex: 10, @int: 4, luk: 4);
        highMax.Should().BeGreaterThan(lowMax);
    }

    [Fact]
    public void Estimate_UsesIntForMagician()
    {
        // Identical stats except the magician's INT vs warrior's STR drive damage.
        var (_, mageMax) = MeleeDamage.Estimate(jobId: 200, level: 30, str: 4, dex: 4, @int: 120, luk: 20);
        var (_, mageLowInt) = MeleeDamage.Estimate(jobId: 200, level: 30, str: 4, dex: 4, @int: 10, luk: 20);
        mageMax.Should().BeGreaterThan(mageLowInt);
    }

    [Fact]
    public void Estimate_UsesLukForThief()
    {
        var (_, hi) = MeleeDamage.Estimate(jobId: 400, level: 30, str: 4, dex: 20, @int: 4, luk: 120);
        var (_, lo) = MeleeDamage.Estimate(jobId: 400, level: 30, str: 4, dex: 20, @int: 4, luk: 10);
        hi.Should().BeGreaterThan(lo);
    }
}
