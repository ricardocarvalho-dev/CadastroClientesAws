using CadastroClientes.Web.Components;
using CadastroClientes.Web.Data;
using Microsoft.AspNetCore.HttpOverrides; // 👈 ADICIONADO
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.CaptureStartupErrors(true).UseSetting("detailedErrors", "true");

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Obtém a String de Conexão do Supabase (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Configura o Identity para usar PostgreSQL (Supabase)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null
        )
    ));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

// URL da API aponta agora para o Elastic Beanstalk (AWS)
builder.Services.AddHttpClient("CadastroAPI", httpClient =>
{
    var apiUri = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";
    httpClient.BaseAddress = new Uri(apiUri);
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// 👇 ADICIONADO — precisa vir logo após o Build(), antes de qualquer outro app.Use...
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseHttpsRedirection();
}
else
{
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


app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        Console.WriteLine(">>> Iniciando EF Core Migrate para Web App no Supabase...");
        logger.LogInformation("Applying migrations to Web App...");
        await db.Database.MigrateAsync();
        Console.WriteLine(">>> EF Core Migrate concluído com sucesso.");
        logger.LogInformation("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> Erro ao aplicar migrations no Supabase: {ex.Message}");
        logger.LogError(ex, "Error applying migrations");
    }
}

app.MapPost("/Account/Logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/Account/Login");
});

app.Run();