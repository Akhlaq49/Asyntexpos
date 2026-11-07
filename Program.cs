using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// Register application services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CatalogImportService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<IWarrantyService, WarrantyService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<ISalesReturnService, SalesReturnService>();
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IFormFieldConfigService, FormFieldConfigService>();

// Tenant Menu Configuration
builder.Services.AddScoped<ITenantMenuService, TenantMenuService>();

// Expenses
builder.Services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

// WhatsApp Cloud API
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();

// AI Agent Service
builder.Services.AddScoped<IAgentToolExecutor, AgentToolExecutor>();
builder.Services.AddHttpClient<IAgentService, AgentService>();

// Drawing to Solution: the Python Drawing2Quote service reads drawings (DrawingService:Url)
builder.Services.AddHttpClient<DrawingService>();

// EF Core - MySQL (Pomelo)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var serverVersion = ServerVersion.AutoDetect(connectionString);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// CORS - allow React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:3001",
                "http://localhost:3002",
                "http://localhost:3000",
                "http://localhost:4173",
                "http://192.168.1.8:3000",
                "http://reactapp.asyntexconsultancy.com",
                "https://reactapp.asyntexconsultancy.com",
                "https://frontapp.asyntexconsultancy.com",
                "http://frontapp.asyntexconsultancy.com",
                "https://aanduwholesellerllc.com",
                "http://aanduwholesellerllc.com",
                "https://OneTouchAutomationPOS.asyntexconsultancy.com",
                "http://OneTouchAutomationPOS.asyntexconsultancy.com",
                "https://faizmart.co.uk",
                "http://faizmart.co.uk"

            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Default port 5000 (can be overridden via ASPNETCORE_URLS env variable)
if (!builder.Configuration.GetSection("ASPNETCORE_URLS").Exists() &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    //builder.WebHost.UseUrls("http://0.0.0.0:5000");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");
// the books are locked up to a date: a change to an entry on or before it is refused with its reason (Services/LedgerPoster.cs)
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (ReactPosApi.Models.LedgerLockedException e) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { message = e.Message });
    }
});
app.UseStaticFiles(); // serve wwwroot/uploads
app.UseAuthentication();
app.UseMiddleware<FrozenAccountMiddleware>(); // a frozen user is signed out on their next request
app.UseAuthorization();
app.MapControllers();

// Database: created from the model when new, then the module scripts (Data/DatabaseSetup.cs); the default company
// and its SuperAdmin are created once
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DatabaseSetup.Apply(db);

    // Use IgnoreQueryFilters() because there's no authenticated user at startup
    var adminExists = db.Parties.IgnoreQueryFilters().NotDeleted().Any(p => p.Email == "admin@reactpos.com" && p.Role == "SuperAdmin");
    if (!adminExists)
    {
        var defaultTenant = new Tenant { Name = "System Admin", Email = "admin@reactpos.com", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Tenants.Add(defaultTenant);
        db.SaveChanges();
        db.Parties.Add(new Party
        {
            FullName = "Admin", Email = "admin@reactpos.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "SuperAdmin", IsActive = true, TenantId = defaultTenant.Id, CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    // default form field configurations of the default company
    var seedTenant = db.Tenants.FirstOrDefault(t => t.Email == "admin@reactpos.com");
    if (seedTenant != null)
        scope.ServiceProvider.GetRequiredService<IFormFieldConfigService>().SeedDefaultsAsync(seedTenant.Id).GetAwaiter().GetResult();
}

app.Run();
