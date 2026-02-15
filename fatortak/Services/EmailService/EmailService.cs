using fatortak.Dtos.Email;
using fatortak.Entities;
using Microsoft.AspNetCore.Identity;
using StudBook.Helpers;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace fatortak.Services.EmailService
{
    public class EmailService : IEmailService
    {
        private UserManager<ApplicationUser> _userManager;
        public EmailService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }
        public async Task<EmailResponseViewModel> ForgotPasswordAsync(ApplicationUser user)
        {
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var resetLink = $"https://fatortak.net/reset-password?userId={user.Id}&token={encodedToken}";

                // Prepare the email
                MailRequest mailRequest = new MailRequest
                {
                    ToEmail = new[] { user.Email },
                    CC = new[] { "ahmednasserr86@gmail.com" },
                    Subject = "Fatortak – Password Reset Request",
                    Body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                      <meta charset='UTF-8'>
                      <style>
                        body {{
                          font-family: Arial, sans-serif;
                          background-color: #f7f9fc;
                          margin: 0;
                          padding: 0;
                          color: #333;
                        }}
                        .container {{
                          max-width: 600px;
                          margin: 40px auto;
                          background: #ffffff;
                          border-radius: 12px;
                          padding: 30px;
                          box-shadow: 0 2px 8px rgba(0,0,0,0.1);
                        }}
                        .header {{
                          text-align: center;
                          margin-bottom: 30px;
                        }}
                        .header img {{
                          max-width: 150px;
                        }}
                        .content p {{
                          line-height: 1.6;
                        }}
                        .button {{
                          display: inline-block;
                          background-color: #4CAF50;
                          color: white !important;
                          padding: 12px 20px;
                          margin: 20px 0;
                          text-decoration: none;
                          border-radius: 6px;
                          font-weight: bold;
                        }}
                        .footer {{
                          margin-top: 30px;
                          font-size: 12px;
                          color: #777;
                          text-align: center;
                        }}
                      </style>
                    </head>
                    <body>
                      <div class='container'>
                        <div class='header'>
                          <img src='https://fatortak.net/logo.png' alt='Fatortak Logo'/>
                        </div>
                        <div class='content'>
                          <p>Hi <strong>{user.UserName}</strong>,</p>
                          <p>We received a request to reset your Fatortak account password. Click the button below to reset it:</p>
                          <p style='text-align:center;'>
                            <a href='{resetLink}' class='button'>Reset Password</a>
                          </p>
                          <p>If the button above doesn’t work, copy and paste this link into your browser:</p>
                          <p><a href='{resetLink}'>{resetLink}</a></p>
                          <p>If you didn’t request this change, you can safely ignore this email.</p>
                          <p>Best regards,<br/>The Fatortak Team</p>
                        </div>
                        <div class='footer'>
                          <p>&copy; {DateTime.UtcNow.Year} Fatortak. All rights reserved.</p>
                        </div>
                      </div>
                    </body>
                    </html>"
                };

                await EmailHelper.SendEmailAsync(mailRequest);

                return new EmailResponseViewModel
                {
                    IsSuccess = true,
                    Message = "Password reset email sent successfully."
                };
            }
            catch (Exception ex)
            {
                return new EmailResponseViewModel
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }


    }
}
