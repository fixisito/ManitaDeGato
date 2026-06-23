using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;

namespace manitaDeGatoWeb.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task EnviarCorreoAsync(string toEmail, string toName, string subject, string bodyHtml)
        {
            var server = _configuration["SmtpSettings:Server"];
            var portStr = _configuration["SmtpSettings:Port"];
            var senderName = _configuration["SmtpSettings:SenderName"];
            var senderEmail = _configuration["SmtpSettings:SenderEmail"];
            var username = _configuration["SmtpSettings:Username"];
            var password = _configuration["SmtpSettings:Password"];

            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(username) || password == "tu_app_password")
            {
                // Evitamos intentar enviar si las credenciales son los placeholders por defecto
                Console.WriteLine($"[EmailService] Envío omitido para {toEmail}. Configure las credenciales reales en appsettings.json.");
                return;
            }

            int port = int.TryParse(portStr, out int p) ? p : 587;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = bodyHtml
            };
            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                // En servidores de prueba o desarrollo a veces es útil ignorar errores de certificados SSL
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                await client.ConnectAsync(server, port, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }
    }
}
