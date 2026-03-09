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

// Register application services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IMiscellaneousRegisterService, MiscellaneousRegisterService>();
builder.Services.AddScoped<IInstallmentService, InstallmentService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISubCategoryService, SubCategoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<IVariantAttributeService, VariantAttributeService>();
builder.Services.AddScoped<IWarrantyService, WarrantyService>();
builder.Services.AddScoped<IStockEntryService, StockEntryService>();
builder.Services.AddScoped<IStockTransferService, StockTransferService>();
builder.Services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<ISalesReturnService, SalesReturnService>();
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IFormFieldConfigService, FormFieldConfigService>();

// Storefront (public online store)
builder.Services.AddScoped<IStorefrontService, StorefrontService>();

// Tenant Menu Configuration
builder.Services.AddScoped<ITenantMenuService, TenantMenuService>();

// HRM Services
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IDesignationService, DesignationService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<ILeaveTypeService, LeaveTypeService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<IHolidayService, HolidayService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();

// Finance & Accounting Services
builder.Services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IIncomeCategoryService, IncomeCategoryService>();
builder.Services.AddScoped<IFinanceIncomeService, FinanceIncomeService>();
builder.Services.AddScoped<IAccountTypeService, AccountTypeService>();
builder.Services.AddScoped<IBankAccountService, BankAccountService>();
builder.Services.AddScoped<IFinanceReportService, FinanceReportService>();

// WhatsApp Cloud API
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();

// AI Agent Service
builder.Services.AddScoped<IAgentToolExecutor, AgentToolExecutor>();
builder.Services.AddHttpClient<IAgentService, AgentService>();

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
                 "http://localhost:3000",
                "http://localhost:4173",
                "http://192.168.1.8:3000",
                "http://reactapp.asyntexconsultancy.com",
                "https://reactapp.asyntexconsultancy.com",
                "https://frontapp.asyntexconsultancy.com",
                "http://frontapp.asyntexconsultancy.com"
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
app.UseStaticFiles(); // serve wwwroot/uploads
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Auto-create the database on first start (Code First - no migrations)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // ── MULTI-TENANCY: Seed default tenant + SuperAdmin ──
    // Use IgnoreQueryFilters() because there's no authenticated user at startup
    var existingAdmin = db.Parties
        .IgnoreQueryFilters()
        .FirstOrDefault(p => p.Email == "admin@reactpos.com" && (p.Role == "SuperAdmin" || p.Role == "Admin"));

    if (existingAdmin == null)
    {
        // Create default tenant
        var defaultTenant = new Tenant
        {
            Name = "System Admin",
            Email = "admin@reactpos.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Tenants.Add(defaultTenant);
        db.SaveChanges();

        // Create SuperAdmin party under the default tenant
        db.Parties.Add(new Party
        {
            FullName = "Admin",
            Email = "admin@reactpos.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "SuperAdmin",
            IsActive = true,
            TenantId = defaultTenant.Id,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
    else
    {
        // Upgrade existing "Admin" seed user to SuperAdmin if needed
        if (existingAdmin.Role == "Admin")
        {
            existingAdmin.Role = "SuperAdmin";
        }

        if (existingAdmin.TenantId == 0)
        {
            // Migration path: existing admin has no tenant — create one
            var tenant = db.Tenants.FirstOrDefault(t => t.Email == "admin@reactpos.com");
            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Name = "System Admin",
                    Email = "admin@reactpos.com",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Tenants.Add(tenant);
                db.SaveChanges();
            }
            existingAdmin.TenantId = tenant.Id;
        }

        db.SaveChanges();
    }

    // Seed default form field configurations for the default tenant
    var seedTenant = db.Tenants.FirstOrDefault(t => t.Email == "admin@reactpos.com");
    if (seedTenant != null)
    {
        var formFieldConfigService = scope.ServiceProvider.GetRequiredService<IFormFieldConfigService>();
        formFieldConfigService.SeedDefaultsAsync(seedTenant.Id).GetAwaiter().GetResult();
    }
}

app.Run();
