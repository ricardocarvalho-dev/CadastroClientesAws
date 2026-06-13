using CadastroClientes.Web.Components;
using CadastroClientes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure logging para Azure
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.CaptureStartupErrors(true).UseSetting("detailedErrors", "true");

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Obtém a String de Conexão segura do Azure (definida nas Environment Variables / Configuration)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Configura o Identity para usar o SQL Server (Azure SQL) do seu cadastrostestes-srv
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ✅ Caminho corrigido para as páginas de Login criadas em /Account/Login
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

// HttpClient para consumir a API utilizando a URL correta mapeada no appsettings.json
builder.Services.AddHttpClient("CadastroAPI", httpClient =>
{
    var apiUri = builder.Configuration["ApiBaseUrl"] ?? "https://ricardodev-solucaoweb-api.azurewebsites.net";
    httpClient.BaseAddress = new Uri(apiUri);
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    // ✅ Tratamento de erro corrigido sem depender de página /Error
    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Erro interno do servidor.");
        });
    });
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// ✅ Autenticação deve vir antes de Autorização
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// Cria/Migra o banco de dados do Identity automaticamente na inicialização no Azure SQL
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        Console.WriteLine(">>> Iniciando EF Core Migrate para Web App no Azure SQL...");
        logger.LogInformation("Applying migrations to Web App...");
        db.Database.Migrate();
        Console.WriteLine(">>> EF Core Migrate concluído com sucesso.");
        logger.LogInformation("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> Erro ao aplicar migrations no Azure SQL: {ex.Message}");
        logger.LogError(ex, "Error applying migrations");
    }
}

app.Run();