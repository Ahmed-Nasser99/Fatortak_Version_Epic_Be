using fatortak.Dtos.Email;
using System.Net.Mail;
using System.Net;

namespace StudBook.Helpers
{
    public static class EmailHelper
    {
        private static string _email;
        private static string _displayName;
        private static string _host;
        private static int _port;
        private static string _password;
        private static bool _isInitialized = false;

        /// <summary>
        /// Call this once at startup (e.g. in Program.cs) to load from appsettings.json
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            _email = configuration["EmailSetting:Email"];
            _displayName = configuration["EmailSetting:DisplayName"];
            _host = configuration["EmailSetting:Host"];
            _port = int.Parse(configuration["EmailSetting:Port"]);
            _password = configuration["EmailSetting:Password"];
            _isInitialized = true;
        }

        public static async Task SendEmailAsync(MailRequest mailRequest)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("EmailHelper not initialized. Call EmailHelper.Initialize(configuration) at startup.");

            try
            {
                using (var emailMessage = new MailMessage())
                {
                    emailMessage.From = new MailAddress(_email, _displayName);

                    if (mailRequest.ToEmail != null)
                    {
                        foreach (var recipient in mailRequest.ToEmail)
                            emailMessage.To.Add(new MailAddress(recipient));
                    }

                    if (mailRequest.CC != null)
                    {
                        foreach (var cc in mailRequest.CC)
                            emailMessage.CC.Add(new MailAddress(cc));
                    }

                    emailMessage.Subject = mailRequest.Subject;
                    emailMessage.Body = mailRequest.Body;
                    emailMessage.IsBodyHtml = true;
                    emailMessage.Priority = MailPriority.Normal;

                    using (var mailClient = new SmtpClient())
                    {
                        mailClient.Host = _host;
                        mailClient.Port = _port;
                        mailClient.EnableSsl = true;
                        mailClient.UseDefaultCredentials = false;

                        // Create credentials with your email and App Password
                        mailClient.Credentials = new NetworkCredential(_email, _password);

                        mailClient.DeliveryMethod = SmtpDeliveryMethod.Network;

                        // For Office 365, these settings are crucial
                        mailClient.Timeout = 30000; // 30 seconds timeout

                        // Send email asynchronously
                        await Task.Run(() => mailClient.Send(emailMessage));
                    }
                }
            }
            catch (SmtpException ex)
            {
                throw new InvalidOperationException($"SMTP error occurred: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Email sending failed: {ex.Message}", ex);
            }
        }
    }
}