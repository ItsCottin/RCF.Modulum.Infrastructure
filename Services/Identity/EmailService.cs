using modulum.Application.Requests.Mail;
using modulum.Shared.Constants.Application;
using modulum.Application.Interfaces.Services.Account;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace modulum.Infrastructure.Services.Identity
{
    public class EmailService : IEmailService
    {


        public async Task<string> SendEmail(MailRequest request)
        {
            try
            {
                var client = new SendGridClient(Environment.GetEnvironmentVariable(ApplicationConstants.Variable.SendGridMailAPIKey));
                var from = new EmailAddress("modulumprojeto@gmail.com", "Modulum");
                var plainTextContent = "and easy to do anywhere, even with C#";
                var htmlContent = "<strong>and easy to do anywhere, even with C#</strong>";
                var msg = MailHelper.CreateSingleEmail(from, new EmailAddress(request.To, "Usuario"), request.Subject, plainTextContent, request.Body);
                var response = await client.SendEmailAsync(msg);
                return "Mail Sent!";
            }
            catch (Exception)
            {

                throw;
            }

        }
    }
}
