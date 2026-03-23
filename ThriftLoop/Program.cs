using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Repositories.Implementation;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.Auth.Implementation;
using ThriftLoop.Services.Auth.Interface;
using ThriftLoop.Services.Email.Implementation;
using ThriftLoop.Services.Email.Interface;
using ThriftLoop.Services.WalletManagement.Implementation;
using ThriftLoop.Services.WalletManagement.Interface;

var builder = WebApplication.CreateBuilder(args);

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Database (EF Core + SQL Server) ──────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)

    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.Cookie.Name = "ThriftLoop.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    })

    .AddCookie("ExternalCookie", options =>
    {
        options.Cookie.Name = "ThriftLoop.External";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    })

    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/signin-google";
        options.SignInScheme = "ExternalCookie";
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.SaveTokens = true;
    });

// ── Dependency Injection ──────────────────────────────────────────────────────

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IEmailService, EmailService>();  // ← email service

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();