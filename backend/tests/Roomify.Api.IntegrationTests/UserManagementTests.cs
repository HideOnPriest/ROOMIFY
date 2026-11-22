using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roomify.Application;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Application.Users;
using Roomify.Infrastructure;
using Roomify.Infrastructure.Identity;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

[Trait("RequiresDocker", "true")]
[Trait("Category", "UserManagement")]
public sealed class UserManagementTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private static string Password() => Guid.NewGuid().ToString("N") + "Aa1";

    [Fact]
    public async Task INT_USR_001_002_003_014_CreateUsesIdentityHashAndOnlyUserRoleWithSafeDto()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);
        await using var provider=Services(database); await using var scope=provider.CreateAsyncScope();
        var password=Password();
        var result=await scope.ServiceProvider.GetRequiredService<CreateUserService>().CreateAsync(new(" New.User@example.invalid "," Nazwa ",password));
        Assert.Equal(UserOperationStatus.Success,result.Status); Assert.True(result.Value!.IsActive); Assert.Equal("Nazwa",result.Value.DisplayName);
        Assert.Equal(MigrationTestData.Clock.GetUtcNow(),result.Value.CreatedAt);
        await using var read=provider.CreateAsyncScope(); var users=read.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored=(await users.FindByIdAsync(result.Value.Id.ToString()))!;
        Assert.NotEqual(password,stored.PasswordHash); Assert.NotNull(stored.PasswordHash); Assert.True(await users.CheckPasswordAsync(stored,password));
        Assert.Equal("NEW.USER@EXAMPLE.INVALID",stored.NormalizedEmail); Assert.Equal(stored.NormalizedEmail,stored.NormalizedUserName);
        Assert.Equal(new[]{IdentityRoles.User},await users.GetRolesAsync(stored));
        var serialized=JsonSerializer.Serialize(result);
        foreach(var secret in new[]{password,stored.PasswordHash!,stored.SecurityStamp!,stored.ConcurrencyStamp!}) Assert.DoesNotContain(secret,serialized);
        var fetched=await read.ServiceProvider.GetRequiredService<GetUserByIdService>().GetAsync(stored.Id);
        Assert.Equal(result.Value,fetched.Value);
        Assert.Equal(1L,await database.ScalarAsync<long>("SELECT count(*) FROM users"));
    }

    [Fact]
    public async Task CreateAndRoleAssignment_RollBackTogetherAndKeepUnrelatedData()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);
        var seed=await MigrationTestData.SeedAsync(database);
        var before=await database.ScalarAsync<string>("SELECT row_to_json(u)::text FROM users u");
        await using var provider=Services(database,failAssignment:true); await using var scope=provider.CreateAsyncScope();
        var result=await scope.ServiceProvider.GetRequiredService<CreateUserService>().CreateAsync(new("rollback@example.invalid",null,Password()));
        Assert.Equal(UserOperationStatus.InfrastructureFailure,result.Status); Assert.Equal("user.roleAssignmentFailed",result.ErrorCode); Assert.Null(result.Value);
        Assert.Equal(before,await database.ScalarAsync<string>("SELECT row_to_json(u)::text FROM users u"));
        Assert.Equal(0L,await database.ScalarAsync<long>("SELECT count(*) FROM roles"));
        Assert.Equal(0L,await database.ScalarAsync<long>("SELECT count(*) FROM user_roles"));
        Assert.Empty(scope.ServiceProvider.GetRequiredService<RoomifyDbContext>().ChangeTracker.Entries());
        Assert.Equal(1L,await database.ScalarAsync<long>("SELECT count(*) FROM rooms")); Assert.NotEqual(Guid.Empty,seed.User.Id);
    }

    [Fact]
    public async Task InvalidPassword_RollsBackRoleBootstrapAndDoesNotLeaveAccount()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);
        await using var provider=Services(database); await using var scope=provider.CreateAsyncScope();
        var result=await scope.ServiceProvider.GetRequiredService<CreateUserService>().CreateAsync(new("weak@example.invalid",null,"x"));
        Assert.Equal(UserOperationStatus.InvalidInput,result.Status); Assert.Equal("user.identityValidation",result.ErrorCode); Assert.Null(result.Value);
        Assert.Equal(0L,await database.ScalarAsync<long>("SELECT count(*) FROM users"));
        Assert.Equal(0L,await database.ScalarAsync<long>("SELECT count(*) FROM roles"));
    }

    [Fact]
    public async Task INT_USR_004_ConcurrentEmailCreatesOneAccountAndPhysicalIndexRejectsBypass()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture); await using var provider=Services(database);
        async Task<UserOperationResult<UserDetails>> Create(string email)
        { await using var scope=provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<CreateUserService>().CreateAsync(new(email,null,Password())); }
        var results=await Task.WhenAll(Create("Same@example.invalid"),Create("SAME@example.invalid"));
        Assert.Single(results,x=>x.Status==UserOperationStatus.Success); Assert.Single(results,x=>x.Status==UserOperationStatus.DuplicateEmail && x.Value is null);
        Assert.Equal(1L,await database.ScalarAsync<long>("SELECT count(*) FROM users")); Assert.Equal(1L,await database.ScalarAsync<long>("SELECT count(*) FROM user_roles"));
        await using var read=database.CreateContext(); var stored=await read.Users.SingleAsync();
        read.Entry(stored).State=EntityState.Detached; stored.Id=Guid.NewGuid();stored.UserName="other@example.invalid";stored.NormalizedUserName="OTHER@EXAMPLE.INVALID";
        read.Users.Add(stored); var error=await Assert.ThrowsAsync<DbUpdateException>(()=>read.SaveChangesAsync());
        var pg=Assert.IsType<PostgresException>(error.InnerException); Assert.Equal("23505",pg.SqlState);Assert.Equal("ux_users_normalized_email",pg.ConstraintName);
    }

    [Fact]
    public async Task INT_USR_005_006_UpdateNormalizesEmailAndLoginPreservingIdTimePasswordAndRoles()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture); await using var provider=Services(database);
        var user=await Create(provider,"before@example.invalid");
        await using var read=database.CreateContext();var before=await read.Users.AsNoTracking().SingleAsync();
        await using var scope=provider.CreateAsyncScope(); var result=await scope.ServiceProvider.GetRequiredService<UpdateUserService>().UpdateAsync(new(user.Id," After@example.invalid "," Nowa nazwa "));
        Assert.Equal(UserOperationStatus.Success,result.Status);Assert.Equal(user.Id,result.Value!.Id);Assert.Equal(user.CreatedAt,result.Value.CreatedAt);
        var after=await read.Users.AsNoTracking().SingleAsync();
        Assert.Equal("After@example.invalid",after.Email);Assert.Equal(after.Email,after.UserName);Assert.Equal("AFTER@EXAMPLE.INVALID",after.NormalizedEmail);
        Assert.Equal(after.NormalizedEmail,after.NormalizedUserName);Assert.Equal("Nowa nazwa",after.DisplayName);
        Assert.Equal(before.PasswordHash,after.PasswordHash);Assert.NotEqual(before.SecurityStamp,after.SecurityStamp);
        Assert.Equal(1L,await database.ScalarAsync<long>("SELECT count(*) FROM user_roles"));
        var other=await Create(provider,"other@example.invalid");
        await using var duplicate=provider.CreateAsyncScope();
        var failed=await duplicate.ServiceProvider.GetRequiredService<UpdateUserService>().UpdateAsync(new(other.Id,"after@example.invalid",null));
        Assert.Equal(UserOperationStatus.DuplicateEmail,failed.Status); Assert.Null(failed.Value);
        Assert.Equal("other@example.invalid",(await read.Users.AsNoTracking().SingleAsync(x=>x.Id==other.Id)).Email);
    }

    [Fact]
    public async Task UpdateEmail_WhenIdentityRejectsLoginAfterEmailSave_RollsBackEveryProfileField()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);await using var provider=Services(database);
        var profile=await Create(provider,"original@example.invalid");
        var before=await database.ScalarAsync<string>("SELECT row_to_json(u)::text FROM users u");
        await using var scope=provider.CreateAsyncScope();
        // Poprawny e-mail zawiera !, którego domyślny walidator UserName Identity nie dopuszcza.
        var result=await scope.ServiceProvider.GetRequiredService<UpdateUserService>().UpdateAsync(new(profile.Id,"valid!tag@example.invalid","Zmiana"));
        Assert.Equal(UserOperationStatus.InvalidInput,result.Status);Assert.Equal("user.identityValidation",result.ErrorCode);Assert.Null(result.Value);
        Assert.Equal(before,await database.ScalarAsync<string>("SELECT row_to_json(u)::text FROM users u"));
        var context=scope.ServiceProvider.GetRequiredService<RoomifyDbContext>();Assert.Empty(context.ChangeTracker.Entries());
        await context.SaveChangesAsync();Assert.Equal(before,await database.ScalarAsync<string>("SELECT row_to_json(u)::text FROM users u"));
    }

    [Fact]
    public async Task ConcurrentUpdate_IdentityStampPreventsSilentOverwriteAndRollsBackPartialEmailChange()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);await using var provider=Services(database);
        var user=await Create(provider,"before@example.invalid");
        await using var first=provider.CreateAsyncScope();await using var second=provider.CreateAsyncScope();
        await first.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id.ToString());
        await second.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id.ToString());
        var results=await Task.WhenAll(first.ServiceProvider.GetRequiredService<UpdateUserService>().UpdateAsync(new(user.Id,"first@example.invalid","A")),
            second.ServiceProvider.GetRequiredService<UpdateUserService>().UpdateAsync(new(user.Id,"second@example.invalid","B")));
        var winner=Assert.Single(results,x=>x.Status==UserOperationStatus.Success);
        Assert.Single(results,x=>x.Status==UserOperationStatus.ConcurrentModification && x.Value is null);
        await using var read=database.CreateContext();var stored=await read.Users.SingleAsync();
        Assert.Equal(winner.Value!.Email,stored.Email);Assert.Equal(winner.Value.DisplayName,stored.DisplayName);Assert.Equal(stored.Email,stored.UserName);
        Assert.Equal(stored.NormalizedEmail,stored.NormalizedUserName);
    }

    [Fact]
    public async Task INT_USR_007_008_009_DeactivateAndActivatePreserveReservationsRolesAndPasswordAndRotateStamp()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);await using var provider=Services(database);
        var profile=await Create(provider,"history@example.invalid");
        var seed=await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database,seed.Room.Id,profile.Id,MigrationTestData.Start,MigrationTestData.Start.AddHours(1));
        await MigrationTestData.InsertAsync(database,seed.Room.Id,profile.Id,MigrationTestData.Start.AddDays(-1000),MigrationTestData.Start.AddDays(-1000).AddHours(1),"cancelled");
        var history=await database.ScalarAsync<string>("SELECT json_agg(r ORDER BY id)::text FROM reservations r");
        await using var read=database.CreateContext();var before=await read.Users.AsNoTracking().SingleAsync(x=>x.Id==profile.Id);
        await using(var scope=provider.CreateAsyncScope())
        {
            var deactivate=scope.ServiceProvider.GetRequiredService<DeactivateUserService>();
            Assert.Equal(UserOperationStatus.Success,(await deactivate.DeactivateAsync(profile.Id)).Status);
            Assert.Equal(UserOperationStatus.AlreadyInactive,(await deactivate.DeactivateAsync(profile.Id)).Status);
        }
        var inactive=await read.Users.AsNoTracking().SingleAsync(x=>x.Id==profile.Id);Assert.False(inactive.IsActive);Assert.NotEqual(before.SecurityStamp,inactive.SecurityStamp);
        Assert.Equal(history,await database.ScalarAsync<string>("SELECT json_agg(r ORDER BY id)::text FROM reservations r"));
        await using(var scope=provider.CreateAsyncScope())
        {
            var activate=scope.ServiceProvider.GetRequiredService<ActivateUserService>();
            Assert.Equal(UserOperationStatus.Success,(await activate.ActivateAsync(profile.Id)).Status);
            Assert.Equal(UserOperationStatus.AlreadyActive,(await activate.ActivateAsync(profile.Id)).Status);
        }
        var after=await read.Users.AsNoTracking().SingleAsync(x=>x.Id==profile.Id);Assert.True(after.IsActive);Assert.Equal(before.Id,after.Id);
        Assert.Equal(before.CreatedAt,after.CreatedAt);Assert.Equal(before.PasswordHash,after.PasswordHash);
        Assert.NotEqual(inactive.SecurityStamp,after.SecurityStamp);
        Assert.Equal(history,await database.ScalarAsync<string>("SELECT json_agg(r ORDER BY id)::text FROM reservations r"));
        Assert.Equal(1L,await database.ScalarAsync<long>("SELECT count(*) FROM user_roles"));
        var manager=provider.CreateAsyncScope();await using(manager)Assert.Equal(new[]{IdentityRoles.User},await manager.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().GetRolesAsync(after));
    }

    [Fact]
    public async Task LastActiveAdministrator_IsProtectedAlsoAgainstConcurrentDeactivation()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);await using var provider=Services(database);
        var first=await Create(provider,"admin1@example.invalid");var second=await Create(provider,"admin2@example.invalid");
        await using(var scope=provider.CreateAsyncScope())
        {
            var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();Assert.True((await roles.CreateAsync(new(IdentityRoles.Administrator){Id=Guid.NewGuid()})).Succeeded);
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach(var id in new[]{first.Id,second.Id})Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(id.ToString()))!,IdentityRoles.Administrator)).Succeeded);
        }
        async Task<UserOperationResult<UserDetails>> Deactivate(Guid id)
        {await using var scope=provider.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<DeactivateUserService>().DeactivateAsync(id);}
        var results=await Task.WhenAll(Deactivate(first.Id),Deactivate(second.Id));
        Assert.Single(results,x=>x.Status==UserOperationStatus.Success);Assert.Single(results,x=>x.Status==UserOperationStatus.LastAdministrator);
        Assert.Equal(1L,await database.ScalarAsync<long>("SELECT count(*) FROM users WHERE is_active"));
        Assert.Equal(4L,await database.ScalarAsync<long>("SELECT count(*) FROM user_roles"));
    }

    [Theory] [InlineData(false,false,true)] [InlineData(true,false,true)] [InlineData(true,true,false)] [InlineData(true,true,true)]
    public async Task INT_USR_012_013_AdministrativeUsecasesRespectTrustedIdentity(bool authenticated,bool administrator,bool active)
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);await using var provider=Services(database,authenticated,administrator,active);
        await using var scope=provider.CreateAsyncScope();var sp=scope.ServiceProvider;var id=Guid.NewGuid();
        var expected=!authenticated?UserOperationStatus.Unauthenticated:!active||!administrator?UserOperationStatus.Forbidden:UserOperationStatus.UserNotFound;
        Assert.Equal(expected,(await sp.GetRequiredService<GetUserByIdService>().GetAsync(id)).Status);
        Assert.Equal(expected,(await sp.GetRequiredService<UpdateUserService>().UpdateAsync(new(id,"update@example.invalid",null))).Status);
        Assert.Equal(expected,(await sp.GetRequiredService<DeactivateUserService>().DeactivateAsync(id)).Status);
        Assert.Equal(expected,(await sp.GetRequiredService<ActivateUserService>().ActivateAsync(id)).Status);
        var create=await sp.GetRequiredService<CreateUserService>().CreateAsync(new("create@example.invalid",null,Password()));
        var allowed=authenticated&&administrator&&active;
        Assert.Equal(allowed?UserOperationStatus.Success:expected,create.Status);
        Assert.Equal(allowed?UserOperationStatus.Success:expected,(await sp.GetRequiredService<GetUsersService>().GetAsync(new())).Status);
        Assert.Equal(allowed?1L:0L,await database.ScalarAsync<long>("SELECT count(*) FROM users"));
    }

    [Theory]
    [InlineData(UserSortBy.Email,UserSortDirection.Ascending)] [InlineData(UserSortBy.Email,UserSortDirection.Descending)]
    [InlineData(UserSortBy.DisplayName,UserSortDirection.Ascending)] [InlineData(UserSortBy.DisplayName,UserSortDirection.Descending)]
    [InlineData(UserSortBy.CreatedAt,UserSortDirection.Ascending)] [InlineData(UserSortBy.CreatedAt,UserSortDirection.Descending)]
    public async Task List_UsesDatabaseFiltersProjectionAndStableBoundedPages(UserSortBy sort,UserSortDirection direction)
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);await using var provider=Services(database);
        var profiles=new List<UserDetails>();for(var i=0;i<5;i++)profiles.Add(await Create(provider,$"user{i}@example.invalid",i<3?"Kowalski":"Nowak"));
        await using(var scope=provider.CreateAsyncScope())await scope.ServiceProvider.GetRequiredService<DeactivateUserService>().DeactivateAsync(profiles[4].Id);
        var sql=new SqlObserver();await using var observed=Services(database,observer:sql);await using var read=observed.CreateAsyncScope();
        var list=read.ServiceProvider.GetRequiredService<GetUsersService>();
        var all=(await list.GetAsync(new(SortBy:sort,SortDirection:direction))).Value!;
        var collected=new List<Guid>();
        for(var page=1;page<=3;page++)
        {
            var request=new GetUsersRequest(page,2,SortBy:sort,SortDirection:direction);
            var result=(await list.GetAsync(request)).Value!;var repeat=(await list.GetAsync(request)).Value!;
            Assert.Equal(result.Items,repeat.Items);Assert.Equal(5,result.TotalCount);collected.AddRange(result.Items.Select(x=>x.Id));
        }
        Assert.Equal(all.Items.Select(x=>x.Id),collected);Assert.Equal(5,collected.Distinct().Count());
        Assert.Equal(3,(await list.GetAsync(new(Search:"KOWALSKI",IsActive:true))).Value!.TotalCount);
        Assert.Equal(profiles[4].Id,Assert.Single((await list.GetAsync(new(Search:"USER",IsActive:false))).Value!.Items).Id);
        Assert.Empty((await list.GetAsync(new(Search:"%_"))).Value!.Items);Assert.Empty((await list.GetAsync(new(99,2))).Value!.Items);
        Assert.Empty(read.ServiceProvider.GetRequiredService<RoomifyDbContext>().ChangeTracker.Entries());
        var query=sql.Commands.First(x=>x.Contains("LIMIT"));Assert.Contains("ORDER BY",query);Assert.Contains("OFFSET",query);
        Assert.Contains(sql.Commands,x=>x.Contains("ILIKE")&&x.Contains("is_active"));
        foreach(var sensitive in new[]{"password_hash","security_stamp","concurrency_stamp","user_tokens"})Assert.DoesNotContain(sensitive,query);
    }

    [Fact]
    public async Task INT_USR_010_011_ActivationRequiresFreshLoginAndOldCookiesNeverReturn()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);await using var provider=Services(database);
        var password=Password();var profile=await Create(provider,"session@example.invalid",password:password);
        using var factory=new UserSessionTestFactory(database.ConnectionString);
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new("https://localhost"),AllowAutoRedirect=false,HandleCookies=false});
        async Task<string> Issue()
        {
            using var response=await client.GetAsync($"/__tests/accounts/issue/{profile.Id}");Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);
            var header=Assert.Single(response.Headers.GetValues("Set-Cookie"),x=>x.StartsWith("Roomify.Auth="));
            Assert.Contains("httponly",header.ToLowerInvariant());Assert.Contains("secure",header.ToLowerInvariant());Assert.Contains("samesite=lax",header.ToLowerInvariant());
            return header.Split(';')[0];
        }
        var cookie=await Issue();client.DefaultRequestHeaders.Add("Cookie",cookie);
        using(var response=await client.GetAsync("/__tests/accounts/private"))
        {Assert.Equal(HttpStatusCode.OK,response.StatusCode);var json=await response.Content.ReadAsStringAsync();Assert.Contains(profile.Id.ToString(),json);Assert.Contains("\"isActive\":true",json);}
        using(var forbidden=await client.GetAsync("/__tests/accounts/admin")){Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);Assert.Null(forbidden.Headers.Location);}
        using(var rooms=await client.GetAsync("/__tests/accounts/rooms"))Assert.Equal(HttpStatusCode.OK,rooms.StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var signIn=scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            Assert.True((await signIn.CheckPasswordSignInAsync((await users.FindByIdAsync(profile.Id.ToString()))!,password,false)).Succeeded);
        }
        await using(var scope=provider.CreateAsyncScope())Assert.Equal(UserOperationStatus.Success,(await scope.ServiceProvider.GetRequiredService<DeactivateUserService>().DeactivateAsync(profile.Id)).Status);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var signIn=scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            var inactive=(await users.FindByIdAsync(profile.Id.ToString()))!;
            Assert.False(await signIn.CanSignInAsync(inactive));Assert.True((await signIn.CheckPasswordSignInAsync(inactive,password,false)).IsNotAllowed);
        }
        using(var rejected=await client.GetAsync("/__tests/accounts/private")){Assert.Equal(HttpStatusCode.Unauthorized,rejected.StatusCode);Assert.Null(rejected.Headers.Location);}
        using(var rejected=await client.GetAsync("/__tests/accounts/rooms"))Assert.Equal(HttpStatusCode.Unauthorized,rejected.StatusCode);
        await using(var scope=provider.CreateAsyncScope())Assert.Equal(UserOperationStatus.Success,(await scope.ServiceProvider.GetRequiredService<ActivateUserService>().ActivateAsync(profile.Id)).Status);
        using(var old=await client.GetAsync("/__tests/accounts/private"))Assert.Equal(HttpStatusCode.Unauthorized,old.StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var signIn=scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            Assert.True((await signIn.CheckPasswordSignInAsync((await users.FindByIdAsync(profile.Id.ToString()))!,password,false)).Succeeded);
        }
        client.DefaultRequestHeaders.Remove("Cookie");var fresh=await Issue();Assert.NotEqual(cookie,fresh);client.DefaultRequestHeaders.Add("Cookie",fresh);
        using(var allowed=await client.GetAsync("/__tests/accounts/private"))Assert.Equal(HttpStatusCode.OK,allowed.StatusCode);
        using(var prod=new RoomifyApiFactory())using(var production=prod.CreateClient())using(var response=await production.GetAsync($"/__tests/accounts/issue/{profile.Id}"))Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }

    [Fact]
    public async Task CookieValidation_ChecksActivityEvenIfSecurityStampWasNotRotatedAndRefreshesTrustedRole()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);await using var provider=Services(database);var profile=await Create(provider,"claims@example.invalid");
        using var factory=new UserSessionTestFactory(database.ConnectionString);using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new("https://localhost"),HandleCookies=false,AllowAutoRedirect=false});
        using var issued=await client.GetAsync($"/__tests/accounts/issue/{profile.Id}");var cookie=issued.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("Roomify.Auth=")).Split(';')[0];client.DefaultRequestHeaders.Add("Cookie",cookie);
        await using(var scope=provider.CreateAsyncScope())
        {
            var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();await roles.CreateAsync(new(IdentityRoles.Administrator){Id=Guid.NewGuid()});
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();await users.AddToRoleAsync((await users.FindByIdAsync(profile.Id.ToString()))!,IdentityRoles.Administrator);
        }
        using(var admin=await client.GetAsync("/__tests/accounts/admin"))Assert.Equal(HttpStatusCode.OK,admin.StatusCode);
        await database.ExecuteAsync("UPDATE users SET is_active=false");
        using(var inactive=await client.GetAsync("/__tests/accounts/private"))Assert.Equal(HttpStatusCode.Unauthorized,inactive.StatusCode);
    }

    [Fact]
    public async Task INT_USR_015_FixtureIsIndependentAndNoProductionLoginOrRegistrationExists()
    {
        await using var database=await MigrationDatabase.CreateAsync(fixture);var connection=new NpgsqlConnectionStringBuilder(database.ConnectionString);
        Assert.StartsWith("roomify_mig_",connection.Database);Assert.Equal("roomify_tests",connection.Username);Assert.NotEqual(5432,connection.Port);
        Assert.Equal(connection.Database,await database.ScalarAsync<string>("SELECT current_database()"));
        using var factory=new RoomifyApiFactory();using var client=factory.CreateClient();
        foreach(var path in new[]{"/api/auth/register","/api/auth/login","/api/auth/logout","/api/users","/__tests/accounts/private"})
        {using var response=await client.PostAsync(path,null);Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);}
    }

    private static async Task<UserDetails> Create(ServiceProvider provider,string email,string? name="Nazwa",string? password=null)
    {
        await using var scope=provider.CreateAsyncScope();var result=await scope.ServiceProvider.GetRequiredService<CreateUserService>().CreateAsync(new(email,name,password??Password()));
        Assert.Equal(UserOperationStatus.Success,result.Status);return result.Value!;
    }

    private static ServiceProvider Services(MigrationDatabase database,bool authenticated=true,bool administrator=true,bool active=true,bool failAssignment=false,SqlObserver? observer=null)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:Roomify"]=database.ConnectionString}).Build();
        var services=new ServiceCollection().AddLogging();services.AddSingleton<ICurrentUser>(new TrustedUser(authenticated?Guid.NewGuid():null,administrator,active));
        services.AddSingleton(MigrationTestData.Clock);services.AddApplication().AddInfrastructure(config);
        if(failAssignment)services.AddScoped<IUserValidator<ApplicationUser>,AssignmentFailureValidator>();
        if(observer is not null)services.ConfigureDbContext<RoomifyDbContext>((_,options)=>options.AddInterceptors(observer));
        return services.BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true});
    }
    private sealed class TrustedUser(Guid? id,bool admin,bool active):ICurrentUser {public Guid? UserId=>id;public bool IsAdministrator=>admin;public bool IsActive=>active;}
    private sealed class AssignmentFailureValidator(RoomifyDbContext context):IUserValidator<ApplicationUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager,ApplicationUser user)
            =>Task.FromResult(user.Email=="rollback@example.invalid"&&context.UserRoles.Local.Any(x=>x.UserId==user.Id)
                ?IdentityResult.Failed(new IdentityError{Code="ProbeAssignmentFailure",Description="Wewnętrzny szczegół nie może opuścić adaptera."}):IdentityResult.Success);
    }
    private sealed class SqlObserver:DbCommandInterceptor
    {
        internal ConcurrentQueue<string> Commands{get;}=new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
        {Commands.Enqueue(command.CommandText);return ValueTask.FromResult(result);}
    }
}
