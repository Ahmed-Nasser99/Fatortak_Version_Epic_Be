
using fatortak.Context;
using fatortak.Entities;
using fatortak.Helpers;
using fatortak.Middlewares;
using fatortak.Seeding;
using fatortak.Services.AuthService;
using fatortak.Services.ChatService;
using fatortak.Services.CompanyService;
using fatortak.Services.CustomerService;
using fatortak.Services.DashboardService;
using fatortak.Services.EmailService;
using fatortak.Services.ExpenseService;
using fatortak.Services.GeminiService;
using fatortak.Services.HR.AttendanceService;
using fatortak.Services.HR.DepartmentService;
using fatortak.Services.HR.EmployeeService;
using fatortak.Services.HR.WorkSettingService;
using fatortak.Services.InvoiceService;
using fatortak.Services.ItemService;
using fatortak.Services.NotificationService;
using fatortak.Services.QuotaService;
using fatortak.Services.ReminderService;
using fatortak.Services.ReportsService;
using fatortak.Services.SubscriptionService;
using fatortak.Services.TenantService;
using fatortak.Services.TokenService;
using fatortak.Services.UserService;
using fatortak.Services.TransactionService;
using fatortak.Services.BackfillService;
using fatortak.Services.ReportService;
using fatortak.Services.HR;
using fatortak.Services.BranchService;
using fatortak.Services.ProjectService;
using fatortak.Services.ProjectService;
using fatortak.Services.AccountingService;
using fatortak.Services.AccountingPostingService;
using fatortak.Services.CustodyService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudBook.Helpers;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

namespace fatortak
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();




            #region Swagger Config
            builder.Services.AddSwaggerGen(options =>
            {
                options.CustomSchemaIds(type => type.FullName); // ? important line
                options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "Fatortak API",
                    Version = "v1"
                });

                options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Description = "Enter JWT token like this: Bearer {your token}"
                });

                options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
            #endregion

            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Configure Data Protection to persist keys in a local directory
            // This ensures tokens (like password reset) remain valid even after application restarts
            var keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys");
            if (!Directory.Exists(keysPath)) Directory.CreateDirectory(keysPath);

            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .SetApplicationName("FatortakApp");

            #region Services registration
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<ICompanyService, CompanyService>();
            builder.Services.AddScoped<IUserProfileService, UserProfileService>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<IInvoiceService, InvoiceService>();
            builder.Services.AddScoped<IItemService, ItemService>();
            builder.Services.AddScoped<IReportsService, ReportsService>();
            builder.Services.AddScoped<ITenantService, TenantService>();
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<IExpenseService, ExpenseService>();
            builder.Services.AddScoped<IQuotaService, QuotaService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
            builder.Services.AddScoped<IEmployeeService, EmployeeService>();
            builder.Services.AddScoped<IDepartmentService, DepartmentService>();
            builder.Services.AddScoped<IAttendanceService, AttendanceService>();
            builder.Services.AddScoped<IWorkSettingService, WorkSettingService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddHttpClient<GeminiService>();
            builder.Services.AddScoped<GeminiService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddHostedService<ReminderGeneratorService>();
            builder.Services.AddHostedService<NotificationCleanupService>();
            builder.Services.AddScoped<ITransactionService, TransactionService>();
            builder.Services.AddScoped<IBackfillService, BackfillService>();
            builder.Services.AddScoped<IReportExportService, ReportExportService>();
            builder.Services.AddScoped<IPayrollService, PayrollService>();
            builder.Services.AddScoped<IBranchService, BranchService>();
            builder.Services.AddScoped<IProjectService, ProjectService>();
            builder.Services.AddScoped<IProjectService, ProjectService>();
            builder.Services.AddScoped<IAccountingService, AccountingService>();
            builder.Services.AddScoped<IAccountingPostingService, AccountingPostingService>();
            builder.Services.AddScoped<ICustodyService, CustodyService>();
            #endregion


            builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(option =>
            {
                // Password settings.
                option.Password.RequireDigit = false;
                option.Password.RequireLowercase = false;
                option.Password.RequireNonAlphanumeric = false;
                option.Password.RequireUppercase = false;
                option.Password.RequiredLength = 6;
                option.Password.RequiredUniqueChars = 0;
            })
             .AddEntityFrameworkStores<ApplicationDbContext>()
             .AddDefaultTokenProviders();


            #region Jwt Config

            var jwtSettings = builder.Configuration.GetSection("Jwt");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });

            #endregion

            var app = builder.Build();
            app.UseCors("AllowAll");

            var httpContextAccessor = app.Services.GetRequiredService<IHttpContextAccessor>();
            UserHelper.Configure(httpContextAccessor);
            EmailHelper.Initialize(builder.Configuration);


            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<TenantResolutionMiddleware>();
            app.UseMiddleware<SubscriptionValidationMiddleware>();

            app.UseStaticFiles();
            app.MapControllers();
            
            // Seed data on startup
            try
            {
                SeedingAdminUser.Seed(app).GetAwaiter().GetResult();
                
                // Seed Chart of Accounts for all existing tenants
                using (var scope = app.Services.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    try
                    {
                        Seeding.AccountSeeder.SeedAccountsAsync(context).GetAwaiter().GetResult();
                        Console.WriteLine("Chart of Accounts seeded successfully for all tenants.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error seeding Chart of Accounts: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during seeding: {ex.Message}");
            }

            app.Run();
        }
    }
}
