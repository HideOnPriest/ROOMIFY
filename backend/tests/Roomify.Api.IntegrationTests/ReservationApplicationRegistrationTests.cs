using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roomify.Application;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Infrastructure;
using Roomify.Infrastructure.Persistence;
using Roomify.Infrastructure.Persistence.Repositories;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "ApplicationComposition")]
[Trait("RequiresDocker", "false")]
public sealed class ReservationApplicationRegistrationTests
{
    [Fact]
    public async Task UnavailableStorage_ReturnsSanitizedInfrastructureFailureDuringRoomRead()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Roomify"] = "Host=127.0.0.1;Port=1;Database=unavailable_test;Username=test;Timeout=1;Command Timeout=1",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(new TrustedUser());
        services.AddSingleton<TimeProvider>(new TestClock());
        services.AddApplication().AddInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(new(
            Guid.NewGuid(), "Spotkanie", new(2030, 1, 15, 9, 0, 0, TimeSpan.Zero), new(2030, 1, 15, 10, 0, 0, TimeSpan.Zero)));
        Assert.Equal(ReservationOperationStatus.InfrastructureFailure, result.Status);
        Assert.Equal("reservation.storageFailure", result.ErrorCode);
        Assert.Null(result.Reservation);
    }

    [Fact]
    public async Task DefaultComposition_IsScopedAndDeniesAccessWithoutQueryingDatabase()
    {
        var services = new ServiceCollection();
        services.AddApplication().AddApplication().AddInfrastructure(new ConfigurationBuilder().Build());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(CreateReservationService));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICurrentUser));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var service = first.ServiceProvider.GetRequiredService<CreateReservationService>();
        var update = first.ServiceProvider.GetRequiredService<UpdateReservationService>();
        var cancel = first.ServiceProvider.GetRequiredService<CancelReservationService>();
        Assert.Same(update, first.ServiceProvider.GetRequiredService<UpdateReservationService>());
        Assert.Same(cancel, first.ServiceProvider.GetRequiredService<CancelReservationService>());
        Assert.NotSame(update, second.ServiceProvider.GetRequiredService<UpdateReservationService>());
        Assert.NotSame(cancel, second.ServiceProvider.GetRequiredService<CancelReservationService>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(UpdateReservationService));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(CancelReservationService));
        Assert.Same(service, first.ServiceProvider.GetRequiredService<CreateReservationService>());
        Assert.NotSame(service, second.ServiceProvider.GetRequiredService<CreateReservationService>());
        Assert.Null(first.ServiceProvider.GetRequiredService<ICurrentUser>().UserId);
        Assert.False(first.ServiceProvider.GetRequiredService<ICurrentUser>().IsAdministrator);
        Assert.IsType<EfRoomRepository>(first.ServiceProvider.GetRequiredService<IRoomRepository>());
        Assert.IsType<EfReservationRepository>(first.ServiceProvider.GetRequiredService<IReservationRepository>());
        Assert.Same(first.ServiceProvider.GetRequiredService<IReservationRepository>(), first.ServiceProvider.GetRequiredService<IReservationRepository>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IReservationRepository>(), second.ServiceProvider.GetRequiredService<IReservationRepository>());
        var result = await service.CreateAsync(null);
        Assert.Equal(ReservationOperationStatus.Unauthenticated, result.Status);
        Assert.Null(result.Reservation);
        Assert.Equal(System.Data.ConnectionState.Closed, first.ServiceProvider.GetRequiredService<RoomifyDbContext>().Database.GetDbConnection().State);
    }

    [Fact]
    public void Composition_PreservesExplicitTrustedIdentityAndClock()
    {
        var services = new ServiceCollection();
        var user = new TrustedUser();
        var clock = new TestClock();
        services.AddSingleton<ICurrentUser>(user);
        services.AddSingleton<TimeProvider>(clock);
        services.AddApplication();
        using var provider = services.BuildServiceProvider();
        Assert.Same(user, provider.GetRequiredService<ICurrentUser>());
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICurrentUser));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    private sealed class TrustedUser : ICurrentUser { public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111"); public bool IsActive => true; }
    private sealed class TestClock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch; }
}
