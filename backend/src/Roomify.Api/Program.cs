using Roomify.Application;
using Roomify.Infrastructure;

// Host wczytuje appsettings.json, konfigurację środowiska, zmienne i argumenty CLI.
// Udostępnia też ILogger z domyślnymi dostawcami logowania, m.in. konsolą.
var builder = WebApplication.CreateBuilder(args);

// DI dostarcza obiektom zależności z kontenera. API składa rejestracje warstw;
// niezależna od infrastruktury warstwa Domain nie wymaga takiej rejestracji.
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Bez kontroli zależności sprawdzamy działanie samej aplikacji, bez bazy.
builder.Services.AddHealthChecks();

var app = builder.Build();

// Middleware obsługuje żądania w kolejności rejestracji. Poza Development
// standardowa obsługa wyjątków nie ujawnia klientowi szczegółów błędu.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

// Profil HTTP działa bez certyfikatu. Profil HTTPS podaje port przekierowania;
// host docelowy może ustawić HTTPS_PORT w konfiguracji środowiska.
if (builder.Configuration.GetValue<int?>("HTTPS_PORT") is not null)
{
    app.UseHttpsRedirection();
}

// Routing dobiera endpoint do ścieżki HTTP. Kontrolery biznesowe powstaną później.
app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health");

app.Logger.LogInformation("Uruchamianie Roomify.Api w środowisku {Environment}.",
    app.Environment.EnvironmentName);

app.Run();

// Publiczny punkt wejścia pozwala WebApplicationFactory uruchomić ten sam host
// w pamięci podczas testów, bez uruchamiania osobnego serwera Kestrel.
public partial class Program { }
