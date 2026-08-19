using FluentAssertions;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Onboarding;
using Xunit;

namespace NoMercy.Tests.OpticalMedia.Onboarding;

[Trait("Category", "Unit")]
public class DiscOnboardingSessionTests
{
    [Fact]
    public void WithState_ReturnsNewInstanceWithUpdatedStateAndTimestamp()
    {
        DiscOnboardingSession session = DiscOnboardingSession.Create("D:\\");
        DiscOnboardingSession next = session.WithState(DiscOnboardingState.Probing);

        next.State.Should().Be(DiscOnboardingState.Probing);
        next.DrivePath.Should().Be("D:\\");
        next.UpdatedAt.Should().BeOnOrAfter(session.UpdatedAt);
        session
            .State.Should()
            .Be(DiscOnboardingState.Detected, "the original instance must be unchanged");
    }

    [Fact]
    public void Create_StartsInDetectedStateWithNoCandidates()
    {
        DiscOnboardingSession session = DiscOnboardingSession.Create("D:\\");

        session.State.Should().Be(DiscOnboardingState.Detected);
        session.Candidates.Should().BeEmpty();
        session.SessionId.Should().NotBe(default(Guid));
    }

    [Fact]
    public void WithCandidates_SetsCandidatesAndState()
    {
        DiscCandidate candidate = new(
            Source: "tmdb",
            StableId: "27205",
            Title: "Inception",
            Year: 2010,
            PosterUrl: null,
            BackdropUrl: null,
            Confidence: 0.95
        );
        DiscOnboardingSession session = DiscOnboardingSession.Create("D:\\");

        DiscOnboardingSession next = session.WithCandidates(
            [candidate],
            DiscOnboardingState.AwaitingConfirm
        );

        next.Candidates.Should().ContainSingle().Which.Title.Should().Be("Inception");
        next.State.Should().Be(DiscOnboardingState.AwaitingConfirm);
    }
}
