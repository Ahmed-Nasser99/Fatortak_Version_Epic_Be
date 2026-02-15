using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Auth;
using fatortak.Dtos.Company;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Services.EmailService;
using fatortak.Services.TenantService;
using fatortak.Services.TokenService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace fatortak.Services.AuthService
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager; // Added missing field
        private readonly ITokenService _tokenService;
        private readonly ITenantService _tenantService;
        private readonly IEmailService _emailServices;
        private readonly ILogger<AuthService> _logger;
        private readonly ApplicationDbContext _context;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager, // Added to constructor
            ITokenService tokenService,
            ITenantService tenantService,
            ILogger<AuthService> logger,
            ApplicationDbContext context,
            IEmailService emailServices)
        {
            _userManager = userManager;
            _roleManager = roleManager; // Initialize the field
            _tokenService = tokenService;
            _tenantService = tenantService;
            _logger = logger;
            _context = context;
            _emailServices = emailServices;
        }

        public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginDto dto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user == null)
                    return ServiceResult<AuthResponseDto>.Failure("Invalid credentials");

                var isValidPassword = await _userManager.CheckPasswordAsync(user, dto.Password);
                if (!isValidPassword)
                    return ServiceResult<AuthResponseDto>.Failure("Invalid credentials");

                // Get user's first tenant (in real app, you might want tenant selection)
                var tenantsResult = await _userManager.Users.Include(u => u.Tenant).Where(u => u.Id == user.Id)
                    .Select(u => u.Tenant)
                    .ToListAsync();
                if (!tenantsResult.Any())
                    return ServiceResult<AuthResponseDto>.Failure("No tenants found for user");

                var primaryTenant = tenantsResult.FirstOrDefault();

                var token = _tokenService.GenerateToken(user, primaryTenant?.Id);

                return ServiceResult<AuthResponseDto>.SuccessResult(new AuthResponseDto
                {
                    Token = token,
                    User = MapToUserDto(user),
                    Tenant = primaryTenant != null ? MapToTenantDto(primaryTenant) : null,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return ServiceResult<AuthResponseDto>.Failure("Login failed");
            }
        }


        public async Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegisterDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Input Validation
                var validationResult = ValidateRegistrationInput(dto);
                if (!validationResult.Success)
                    return validationResult;

                // 2. Check for Existing User
                var existingUser = await _userManager.FindByEmailAsync(dto.Email);
                if (existingUser != null)
                    return ServiceResult<AuthResponseDto>.Failure("Email already registered");

                // 3. Check subdomain uniqueness (moved from TenantService to avoid duplication)
                if (!string.IsNullOrEmpty(dto.Subdomain))
                {
                    var subdomainExists = await _context.Tenants.AnyAsync(t => t.Subdomain == dto.Subdomain);
                    if (subdomainExists)
                        return ServiceResult<AuthResponseDto>.Failure("Subdomain already taken");
                }

                // 4. Create User Account
                var userCreationResult = await CreateUserAccount(dto);
                if (!userCreationResult.Success)
                {
                    return ServiceResult<AuthResponseDto>.Failure(userCreationResult.ErrorMessage);
                }

                var user = userCreationResult.Data;
                // 5. Create Tenant (Company) - Using simplified approach to avoid duplication
                var tenantCreationResult = await CreateTenantForUserSimple(dto, user.Id);
                if (!tenantCreationResult.Success)
                {
                    await _userManager.DeleteAsync(user);
                    return ServiceResult<AuthResponseDto>.Failure(tenantCreationResult.ErrorMessage);
                }

                var tenant = tenantCreationResult.Data;

                // 7. Create Default Company Profile (if not already created by tenant creation)
                var company = await _context.Companies.FirstOrDefaultAsync(c => c.TenantId == tenant.Id);
                if (company == null)
                {
                    var companyCreationResult = await CreateDefaultCompany(tenant.Id, dto);
                    if (!companyCreationResult.Success)
                    {
                        await DeleteTenantAndRelatedData(tenant.Id);
                        await _userManager.DeleteAsync(user);
                        return ServiceResult<AuthResponseDto>.Failure(companyCreationResult.ErrorMessage);
                    }
                    company = companyCreationResult.Data;
                }


                var subscription = await _context.Subscriptions.FirstOrDefaultAsync(c => c.TenantId == tenant.Id);
                if (subscription == null)
                {
                    var subscriptionCreationResult = await CreateTrailSubscription(tenant.Id, dto);
                    if (!subscriptionCreationResult.Success)
                    {
                        await DeleteTenantAndRelatedData(tenant.Id);
                        await _userManager.DeleteAsync(user);
                        return ServiceResult<AuthResponseDto>.Failure(subscriptionCreationResult.ErrorMessage);
                    }
                }

                // 8. Generate JWT Token
                var token = _tokenService.GenerateToken(user, tenant.Id);

                // 9. Send Welcome Email (fire-and-forget)
                _ = SendWelcomeEmailAsync(user, tenant);

                await transaction.CommitAsync();

                return ServiceResult<AuthResponseDto>.SuccessResult(new AuthResponseDto
                {
                    Token = token,
                    User = MapToUserDto(user),
                    Tenant = MapToTenantDto(tenant),
                    Company = MapToCompanyDto(company)
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during registration");
                return ServiceResult<AuthResponseDto>.Failure("Registration failed. Please try again.");
            }
        }


        #region Create Profile When Register
        private async Task<ServiceResult<ApplicationUser>> CreateUserAccount(RegisterDto dto)
        {
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                EmailConfirmed = false, // Require email confirmation
                IsActive = true,
                Role = RoleEnum.Admin.ToString()
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return ServiceResult<ApplicationUser>.ValidationError(errors);
            }

            return ServiceResult<ApplicationUser>.SuccessResult(user);
        }

        // Simplified tenant creation to avoid duplication with TenantService
        private async Task<ServiceResult<Tenant>> CreateTenantForUserSimple(RegisterDto dto, Guid userId)
        {
            try
            {
                var tenant = new Tenant
                {
                    Name = dto.CompanyName,
                    Subdomain = dto?.Subdomain ?? null,
                    IsActive = true
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();

                // Create default Main Branch
                var branch = new Branch
                {
                    TenantId = tenant.Id,
                    Name = "Main Branch",
                    IsMain = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user != null)
                {
                    user.TenantId = tenant.Id;
                    await _userManager.UpdateAsync(user);
                }



                return ServiceResult<Tenant>.SuccessResult(tenant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tenant");
                return ServiceResult<Tenant>.Failure("Failed to create tenant");
            }
        }

        private async Task<ServiceResult<Company>> CreateDefaultCompany(Guid tenantId, RegisterDto dto)
        {
            try
            {
                var company = new Company
                {
                    TenantId = tenantId,
                    Name = dto.CompanyName,
                    Email = dto.Email,
                    Phone = dto.PhoneNumber ?? string.Empty,
                    Address = dto.Address ?? string.Empty,
                    Currency = dto.Currency,
                    DefaultVatRate = 0.2m,
                    InvoicePrefix = "INV-",
                    IsActive = true
                };

                await _context.Companies.AddAsync(company);
                await _context.SaveChangesAsync();

                return ServiceResult<Company>.SuccessResult(company);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating default company");
                return ServiceResult<Company>.Failure("Failed to create company profile");
            }
        }


        private async Task<ServiceResult<Subscription>> CreateTrailSubscription(Guid tenantId, RegisterDto dto)
        {
            try
            {
                var subscription = new Subscription
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Plan = SubscriptionPlan.Trial,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(14),
                    IsYearly = false,
                    AiUsageThisMonth = 0,
                    AiUsageResetDate = null
                };

                await _context.Subscriptions.AddAsync(subscription);
                await _context.SaveChangesAsync();

                return ServiceResult<Subscription>.SuccessResult(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating default company");
                return ServiceResult<Subscription>.Failure("Failed to create company profile");
            }
        }

        // Helper method to clean up tenant and related data
        private async Task DeleteTenantAndRelatedData(Guid tenantId)
        {
            try
            {
                _userManager.Users.Where(u => u.TenantId == tenantId).ToList().ForEach(user =>
                 {
                     user.TenantId = null;
                     _userManager.UpdateAsync(user).Wait();
                 });

                // Remove companies
                var companies = await _context.Companies.Where(c => c.TenantId == tenantId).ToListAsync();
                _context.Companies.RemoveRange(companies);


                var subscriptions = await _context.Subscriptions.Where(c => c.TenantId == tenantId).ToListAsync();
                _context.Subscriptions.RemoveRange(subscriptions);

                // Remove tenant
                var tenant = await _context.Tenants.FindAsync(tenantId);
                if (tenant != null)
                {
                    _context.Tenants.Remove(tenant);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up tenant data during rollback");
            }
        }
        #endregion

        public async Task<ServiceResult<string>> ForgetPasswordRequestAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return ServiceResult<string>.Failure("User Not Found");

            var emailResponse = await _emailServices.ForgotPasswordAsync(user);
            if (!emailResponse.IsSuccess)
                return ServiceResult<string>.Failure("Sending Email Failed");

            return ServiceResult<string>.SuccessResult("Email Sent Successfully");
        }

        public async Task<ServiceResult<string>> SetNewPassword(string userId, string token, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult<string>.Failure("No User Found");

            // Robust decoding: Base64UrlDecode ensures special characters (+, /, =) are handled correctly
            try
            {
                var decodedTokenBytes = WebEncoders.Base64UrlDecode(token);
                token = Encoding.UTF8.GetString(decodedTokenBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decode password reset token for user {UserId}", userId);
                return ServiceResult<string>.Failure("Invalid Reset Link Format");
            }

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Password reset failed for user {UserId}: {Errors}", userId, errors);
                return ServiceResult<string>.Failure("Password reset failed: " + errors);
            }

            user.IsActive = true;
            await _userManager.UpdateAsync(user);

            return ServiceResult<string>.SuccessResult("Password reset successfully.");
        }


        private async Task SendWelcomeEmailAsync(ApplicationUser user, Tenant tenant)
        {
            try
            {
                var emailBody = $@"
            <h1>Welcome to Our Platform, {user.FirstName}!</h1>
            <p>Your company <strong>{tenant.Name}</strong> has been successfully registered.</p>
            <p>You can now access your dashboard at: {tenant.Subdomain}.yourapp.com</p>
        ";

                // await _emailService.SendEmailAsync(
                //     user.Email,
                //     "Welcome to Our Platform",
                //     emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending welcome email");
            }
        }

        private ServiceResult<AuthResponseDto> ValidateRegistrationInput(RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return ServiceResult<AuthResponseDto>.Failure("Email is required");

            if (!new EmailAddressAttribute().IsValid(dto.Email))
                return ServiceResult<AuthResponseDto>.Failure("Invalid email format");

            if (string.IsNullOrWhiteSpace(dto.Password))
                return ServiceResult<AuthResponseDto>.Failure("Password is required");

            if (dto.Password.Length < 8)
                return ServiceResult<AuthResponseDto>.Failure("Password must be at least 8 characters");

            if (string.IsNullOrWhiteSpace(dto.CompanyName))
                return ServiceResult<AuthResponseDto>.Failure("Company name is required");

            return ServiceResult<AuthResponseDto>.SuccessResult(null);
        }

        // Helper methods
        private UserDto MapToUserDto(ApplicationUser user) => new()
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role
        };

        private TenantDto MapToTenantDto(Tenant tenant) => new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Subdomain = tenant.Subdomain
        };

        // Added missing MapToCompanyDto method
        private CompanyDto MapToCompanyDto(Company company) => new()
        {
            Id = company.Id,
            Name = company.Name,
            Email = company.Email,
            Phone = company.Phone,
            Currency = company.Currency,
            DefaultVatRate = company.DefaultVatRate,
            InvoicePrefix = company.InvoicePrefix
        };
    }
}