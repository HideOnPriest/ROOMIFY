using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roomify.Application;
using Roomify.Application.Rooms;
using Roomify.Infrastructure;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

[Trait("RequiresDocker", "false")]
public sealed class RoomApplicationRegistrationTests
{
    [Fact]
    public async Task SixServices_AreScopedRegisteredOnceAndDefaultIdentityDeniesWithoutSql()
    {
        var services = new ServiceCollection().AddApplication().AddApplication().AddInfrastructure(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var first = provider.CreateAsyncScope(); await using var second = provider.CreateAsyncScope();
        foreach (var type in new[] { typeof(CreateRoomService), typeof(UpdateRoomService), typeof(ArchiveRoomService),
            typeof(ActivateRoomService), typeof(GetRoomByIdService), typeof(GetRoomsService) })
        {
            Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, x => x.ServiceType == type).Lifetime);
            var resolved = first.ServiceProvider.GetRequiredService(type);
            Assert.Same(resolved, first.ServiceProvider.GetRequiredService(type));
            Assert.NotSame(resolved, second.ServiceProvider.GetRequiredService(type));
        }
        var sp = first.ServiceProvider;
        Assert.Equal(RoomOperationStatus.Unauthenticated, (await sp.GetRequiredService<CreateRoomService>().CreateAsync(null)).Status);
        Assert.Equal(RoomOperationStatus.Unauthenticated, (await sp.GetRequiredService<UpdateRoomService>().UpdateAsync(null)).Status);
        Assert.Equal(RoomOperationStatus.Unauthenticated, (await sp.GetRequiredService<ArchiveRoomService>().ArchiveAsync(Guid.NewGuid())).Status);
        Assert.Equal(RoomOperationStatus.Unauthenticated, (await sp.GetRequiredService<ActivateRoomService>().ActivateAsync(Guid.NewGuid())).Status);
        Assert.Equal(RoomOperationStatus.Unauthenticated, (await sp.GetRequiredService<GetRoomByIdService>().GetAsync(Guid.NewGuid())).Status);
        Assert.Equal(RoomOperationStatus.Unauthenticated, (await sp.GetRequiredService<GetRoomsService>().GetAsync(new())).Status);
        Assert.Equal(ConnectionState.Closed, sp.GetRequiredService<RoomifyDbContext>().Database.GetDbConnection().State);
    }
}
