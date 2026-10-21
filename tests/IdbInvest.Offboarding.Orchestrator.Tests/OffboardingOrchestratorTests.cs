using IdbInvest.Offboarding.Orchestrator.Application.Services;
using IdbInvest.Offboarding.Orchestrator.Core.Exceptions;
using IdbInvest.Offboarding.Orchestrator.Core.Interfaces;
using IdbInvest.Offboarding.Orchestrator.Core.Models;

namespace IdbInvest.Offboarding.Orchestrator.Tests;

public sealed class OffboardingOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_CompletesWhenAllTargetsSucceed()
    {
        var candidate = new OffboardingCandidate("E100", "user@iadb.org", new DateOnly(2026, 10, 15));
        var provider = new FakeCandidateProvider(candidate);
        var state = new InMemoryStateStore();
        var peopleSoft = new FakeAdapter(TargetSystem.PeopleSoft, success: true);
        var acbs = new FakeAdapter(TargetSystem.Acbs, success: true);

        var sut = new OffboardingOrchestrator(provider, [peopleSoft, acbs], state);

        var result = await sut.ExecuteAsync(
            new StartOrchestrationCommand("E100", [TargetSystem.Acbs, TargetSystem.PeopleSoft]),
            CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal(OrchestrationStatus.Completed, result.Orchestration.Status);
        Assert.Equal(2, result.Orchestration.Targets.Count);
        Assert.Equal(1, peopleSoft.CallCount);
        Assert.Equal(1, acbs.CallCount);
        Assert.Equal(2, state.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsExistingResultForDuplicateRequest()
    {
        var candidate = new OffboardingCandidate("E100", "user@iadb.org", new DateOnly(2026, 10, 15));
        var provider = new FakeCandidateProvider(candidate);
        var state = new InMemoryStateStore();
        var adapter = new FakeAdapter(TargetSystem.Acbs, success: true);
        var sut = new OffboardingOrchestrator(provider, [adapter], state);
        var command = new StartOrchestrationCommand("E100", [TargetSystem.Acbs], "request-123");

        var first = await sut.ExecuteAsync(command, CancellationToken.None);
        var second = await sut.ExecuteAsync(command, CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(first.Orchestration.OrchestrationId, second.Orchestration.OrchestrationId);
        Assert.Equal(1, adapter.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPartiallyCompletedWhenOneTargetFails()
    {
        var candidate = new OffboardingCandidate("E100", "user@iadb.org", new DateOnly(2026, 10, 15));
        var sut = new OffboardingOrchestrator(
            new FakeCandidateProvider(candidate),
            [
                new FakeAdapter(TargetSystem.PeopleSoft, success: true),
                new FakeAdapter(TargetSystem.Workiva, success: false)
            ],
            new InMemoryStateStore());

        var result = await sut.ExecuteAsync(
            new StartOrchestrationCommand(
                "E100",
                [TargetSystem.PeopleSoft, TargetSystem.Workiva]),
            CancellationToken.None);

        Assert.Equal(OrchestrationStatus.PartiallyCompleted, result.Orchestration.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenCandidateDoesNotExist()
    {
        var sut = new OffboardingOrchestrator(
            new FakeCandidateProvider(null),
            [new FakeAdapter(TargetSystem.Acbs, success: true)],
            new InMemoryStateStore());

        await Assert.ThrowsAsync<OffboardingCandidateNotFoundException>(() =>
            sut.ExecuteAsync(
                new StartOrchestrationCommand("UNKNOWN", [TargetSystem.Acbs]),
                CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenTargetAdapterIsNotRegistered()
    {
        var candidate = new OffboardingCandidate("E100", "user@iadb.org", new DateOnly(2026, 10, 15));
        var sut = new OffboardingOrchestrator(
            new FakeCandidateProvider(candidate),
            [new FakeAdapter(TargetSystem.Acbs, success: true)],
            new InMemoryStateStore());

        var ex = await Assert.ThrowsAsync<InvalidOrchestrationRequestException>(() =>
            sut.ExecuteAsync(
                new StartOrchestrationCommand("E100", [TargetSystem.Workiva]),
                CancellationToken.None));

        Assert.Contains("Workiva", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_ReturnsSavedOrchestration()
    {
        var candidate = new OffboardingCandidate("E100", "user@iadb.org", new DateOnly(2026, 10, 15));
        var state = new InMemoryStateStore();
        var sut = new OffboardingOrchestrator(
            new FakeCandidateProvider(candidate),
            [new FakeAdapter(TargetSystem.PeopleSoft, success: true)],
            state);

        var execution = await sut.ExecuteAsync(
            new StartOrchestrationCommand("E100", [TargetSystem.PeopleSoft]),
            CancellationToken.None);

        var result = await sut.GetAsync(
            execution.Orchestration.OrchestrationId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(execution.Orchestration.OrchestrationId, result.OrchestrationId);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenOrchestrationDoesNotExist()
    {
        var sut = new OffboardingOrchestrator(
            new FakeCandidateProvider(null),
            [new FakeAdapter(TargetSystem.PeopleSoft, success: true)],
            new InMemoryStateStore());

        var result = await sut.GetAsync("missing", CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_RejectsMissingEmployeeId(string employeeId)
    {
        var sut = new OffboardingOrchestrator(
            new FakeCandidateProvider(null),
            [new FakeAdapter(TargetSystem.Acbs, success: true)],
            new InMemoryStateStore());

        await Assert.ThrowsAsync<InvalidOrchestrationRequestException>(() =>
            sut.ExecuteAsync(
                new StartOrchestrationCommand(employeeId, [TargetSystem.Acbs]),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetReadinessAsync_ReturnsReadyWhenDependenciesAreAvailable()
    {
        var sut = new OffboardingOrchestrator(
            new FakeCandidateProvider(null, available: true),
            [new FakeAdapter(TargetSystem.Acbs, success: true)],
            new InMemoryStateStore(available: true));

        var result = await sut.GetReadinessAsync(CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.True(result.DataExposureAvailable);
        Assert.True(result.StateStoreAvailable);
    }

    [Fact]
    public async Task GetReadinessAsync_ReturnsDegradedWhenAnyDependencyIsUnavailable()
    {
        var sut = new OffboardingOrchestrator(
            new FakeCandidateProvider(null, available: false),
            [new FakeAdapter(TargetSystem.Acbs, success: true)],
            new InMemoryStateStore(available: true));

        var result = await sut.GetReadinessAsync(CancellationToken.None);

        Assert.False(result.IsReady);
        Assert.False(result.DataExposureAvailable);
        Assert.True(result.StateStoreAvailable);
    }

    private sealed class FakeCandidateProvider(
        OffboardingCandidate? candidate,
        bool available = true)
        : IOffboardingCandidateProvider
    {
        public Task<OffboardingCandidate?> GetByEmployeeIdAsync(
            string employeeId,
            CancellationToken cancellationToken)
            => Task.FromResult(candidate);

        public Task<bool> PingAsync(CancellationToken cancellationToken)
            => Task.FromResult(available);
    }

    private sealed class FakeAdapter(TargetSystem target, bool success)
        : ITargetOffboardingAdapter
    {
        public TargetSystem Target { get; } = target;
        public int CallCount { get; private set; }

        public Task<TargetExecutionResult> ExecuteAsync(
            OffboardingCandidate candidate,
            string orchestrationId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new TargetExecutionResult(
                Target,
                success,
                success ? "Completed" : "Failed",
                success ? null : "Simulated failure"));
        }
    }

    private sealed class InMemoryStateStore(bool available = true) : IOrchestrationStateStore
    {
        private readonly Dictionary<string, OffboardingOrchestration> _items =
            new(StringComparer.OrdinalIgnoreCase);

        public int SaveCount { get; private set; }

        public Task<OffboardingOrchestration?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            _items.TryGetValue(idempotencyKey, out var value);
            return Task.FromResult(value);
        }

        public Task<OffboardingOrchestration?> GetByIdAsync(
            string orchestrationId,
            CancellationToken cancellationToken)
        {
            var value = _items.Values.FirstOrDefault(
                x => x.OrchestrationId.Equals(
                    orchestrationId,
                    StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(value);
        }

        public Task SaveAsync(
            OffboardingOrchestration orchestration,
            CancellationToken cancellationToken)
        {
            _items[orchestration.IdempotencyKey] = orchestration;
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<bool> PingAsync(CancellationToken cancellationToken)
            => Task.FromResult(available);
    }
}
