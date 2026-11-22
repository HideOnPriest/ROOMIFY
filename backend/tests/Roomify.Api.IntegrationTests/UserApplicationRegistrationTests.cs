using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Roomify.Application;
using Roomify.Application.Abstractions;
using Roomify.Application.Users;
using Roomify.Infrastructure;
using Roomify.Infrastructure.Identity;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

[Trait("RequiresDocker","false")]
public sealed class UserApplicationRegistrationTests
{
    [Fact]
    public async Task UnavailableDatabase_ProducesNeutralFailuresAndCancellationRemainsCancellation()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["ConnectionStrings:Roomify"]="Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Timeout=1;Command Timeout=1" }).Build();
        var services=new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(config);
        await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();
        var store=scope.ServiceProvider.GetRequiredService<IUserAccountStore>();
        var found=await store.FindAsync(Guid.NewGuid(),default);
        Assert.Equal(UserOperationStatus.InfrastructureFailure,found.Status);Assert.Null(found.Value);Assert.Equal("user.storageFailure",found.ErrorCode);
        var list=await store.SearchAsync(new(0,20,null,null,UserSortBy.Email,UserSortDirection.Ascending),default);
        Assert.Equal(UserOperationStatus.InfrastructureFailure,list.Status);Assert.Null(list.Value);
        var created=await store.CreateAsync(new("test@example.invalid",null,Guid.NewGuid()+"Aa1"),default);
        Assert.Equal(UserOperationStatus.InfrastructureFailure,created.Status);Assert.Null(created.Value);
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.CreateAsync(new("test@example.invalid",null,"unused"),cancelled.Token));
    }

    [Fact]
    public async Task SixServicesAndOneIdentityAdapter_AreScopedAndDefaultHostDeniesWithoutDatabase()
    {
        var services=new ServiceCollection().AddLogging().AddApplication().AddApplication().AddInfrastructure(new ConfigurationBuilder().Build());
        await using var provider=services.BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true});
        await using var first=provider.CreateAsyncScope();await using var second=provider.CreateAsyncScope();
        foreach(var type in new[]{typeof(CreateUserService),typeof(UpdateUserService),typeof(GetUsersService),typeof(GetUserByIdService),typeof(ActivateUserService),typeof(DeactivateUserService),typeof(IUserAccountStore)})
        {
            Assert.Equal(ServiceLifetime.Scoped,Assert.Single(services,x=>x.ServiceType==type).Lifetime);
            Assert.Same(first.ServiceProvider.GetRequiredService(type),first.ServiceProvider.GetRequiredService(type));
            Assert.NotSame(first.ServiceProvider.GetRequiredService(type),second.ServiceProvider.GetRequiredService(type));
        }
        var sp=first.ServiceProvider;
        Assert.IsType<IdentityUserAccountStore>(sp.GetRequiredService<IUserAccountStore>());
        Assert.IsType<HttpIdentityCurrentUser>(sp.GetRequiredService<ICurrentUser>());
        Assert.Null(sp.GetRequiredService<ICurrentUser>().UserId);Assert.False(sp.GetRequiredService<ICurrentUser>().IsActive);
        Assert.IsType<ActiveAccountSignInManager>(sp.GetRequiredService<SignInManager<ApplicationUser>>());
        Assert.IsType<ActiveAccountStampValidator>(sp.GetRequiredService<ISecurityStampValidator>());
        Assert.Equal(TimeSpan.Zero,sp.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval);
        Assert.Equal(UserOperationStatus.Unauthenticated,(await sp.GetRequiredService<CreateUserService>().CreateAsync(null)).Status);
        Assert.Equal(UserOperationStatus.Unauthenticated,(await sp.GetRequiredService<UpdateUserService>().UpdateAsync(null)).Status);
        Assert.Equal(UserOperationStatus.Unauthenticated,(await sp.GetRequiredService<GetUsersService>().GetAsync(null)).Status);
        Assert.Equal(UserOperationStatus.Unauthenticated,(await sp.GetRequiredService<GetUserByIdService>().GetAsync(Guid.Empty)).Status);
        Assert.Equal(UserOperationStatus.Unauthenticated,(await sp.GetRequiredService<DeactivateUserService>().DeactivateAsync(Guid.Empty)).Status);
        Assert.Equal(UserOperationStatus.Unauthenticated,(await sp.GetRequiredService<ActivateUserService>().ActivateAsync(Guid.Empty)).Status);
        Assert.Equal(ConnectionState.Closed,sp.GetRequiredService<RoomifyDbContext>().Database.GetDbConnection().State);
        Assert.Single(services,x=>x.ServiceType==typeof(RoomifyDbContext));
    }
}
