using modulum.Application.Requests.Mail;
using modulum.Shared.Constants.Application;
using modulum.Application.Interfaces.Services.Account;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace modulum.Infrastructure.Services.Identity
{
    public class EmailService : IEmailService
    {

        public string GetContent = "<!doctype html>\r\n<html lang=\"pt-BR\">\r\n<head>\r\n\t<meta content=\"text/html; charset=utf-8\" http-equiv=\"Content-Type\" />\r\n</head>\r\n<body marginheight=\"0\" topmargin=\"0\" marginwidth=\"0\" style=\"margin: 0px; background-color: #f2f3f8;\" leftmargin=\"0\">\r\n\t<table cellspacing=\"0\" border=\"0\" cellpadding=\"0\" width=\"100%\" bgcolor=\"#f2f3f8\"\r\n\t\tstyle=\"@import url(https://fonts.googleapis.com/css?family=Rubik:300,400,500,700|Open+Sans:300,400,600,700); font-family: 'Open Sans', sans-serif;\">\r\n\t\t<tr>\r\n\t\t\t<td>\r\n\t\t\t\t<table style=\"background-color: #f2f3f8; max-width:670px;  margin:0 auto;\" width=\"100%\" border=\"0\" align=\"center\" cellpadding=\"0\" cellspacing=\"0\">\r\n\t\t\t\t\t<tr>\r\n\t\t\t\t\t\t<td style=\"height:80px;\">&nbsp;</td>\r\n\t\t\t\t\t</tr>\r\n\t\t\t\t\t<tr>\r\n\t\t\t\t\t\t<td>\r\n\t\t\t\t\t\t\t<table width=\"95%\" border=\"0\" align=\"center\" cellpadding=\"0\" cellspacing=\"0\"\r\n\t\t\t\t\t\t\t\tstyle=\"max-width:670px;background:#fff; border-radius:3px; text-align:center;-webkit-box-shadow:0 6px 18px 0 rgba(0,0,0,.06);-moz-box-shadow:0 6px 18px 0 rgba(0,0,0,.06);box-shadow:0 6px 18px 0 rgba(0,0,0,.06);\">\r\n\t\t\t\t\t\t\t\t<tr>\r\n\t\t\t\t\t\t\t\t\t<td style=\"height:40px;\">&nbsp;</td>\r\n\t\t\t\t\t\t\t\t</tr>\r\n\t\t\t\t\t\t\t\t<tr>\r\n\t\t\t\t\t\t\t\t\t<td style=\"padding:0 35px;\">\r\n\t\t\t\t\t\t\t\t\t\t<h1 style=\"color:#1e1e2d; font-weight:500; margin:0;font-size:32px;font-family:'Rubik',sans-serif;\">\r\n\t\t\t\t\t\t\t\t\t\t\tVerificação</h1>\r\n\t\t\t\t\t\t\t\t\t\t<span\r\n\t\t\t\t\t\t\t\t\t\t\tstyle=\"display:inline-block; vertical-align:middle; margin:29px 0 26px; border-bottom:1px solid #cecece; width:100px;\"></span>\r\n\t\t\t\t\t\t\t\t\t\t<p style=\"color:#455056; font-size:15px;line-height:24px; margin:0;\">\r\n\t\t\t\t\t\t\t\t\t\t\tInsira o código abaixo no sistema continuar:\r\n\t\t\t\t\t\t\t\t\t\t</p>\r\n\t\t\t\t\t\t\t\t\t\t<div style=\"margin-top:30px; display:flex; justify-content:center; gap:10px;\">\r\n\t\t\t\t\t\t\t\t\t\t\t<span style=\"display:inline-block; width:40px; height:50px; font-size:24px; line-height:50px; text-align:center; background:#f0f0f0; border-radius:8px; font-weight:bold;\">cod1</span>\r\n\t\t\t\t\t\t\t\t\t\t\t<span style=\"display:inline-block; width:40px; height:50px; font-size:24px; line-height:50px; text-align:center; background:#f0f0f0; border-radius:8px; font-weight:bold;\">cod2</span>\r\n\t\t\t\t\t\t\t\t\t\t\t<span style=\"display:inline-block; width:40px; height:50px; font-size:24px; line-height:50px; text-align:center; background:#f0f0f0; border-radius:8px; font-weight:bold;\">cod3</span>\r\n\t\t\t\t\t\t\t\t\t\t\t<span style=\"display:inline-block; width:40px; height:50px; font-size:24px; line-height:50px; text-align:center; background:#f0f0f0; border-radius:8px; font-weight:bold;\">cod4</span>\r\n\t\t\t\t\t\t\t\t\t\t\t<span style=\"display:inline-block; width:40px; height:50px; font-size:24px; line-height:50px; text-align:center; background:#f0f0f0; border-radius:8px; font-weight:bold;\">cod5</span>\r\n\t\t\t\t\t\t\t\t\t\t\t<span style=\"display:inline-block; width:40px; height:50px; font-size:24px; line-height:50px; text-align:center; background:#f0f0f0; border-radius:8px; font-weight:bold;\">cod6</span>\r\n\t\t\t\t\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t\t\t\t\t<p style=\"margin-top:25px; font-size:14px; color:#777;\">\r\n\t\t\t\t\t\t\t\t\t\t\tO código expira em 10 minutos.\r\n\t\t\t\t\t\t\t\t\t\t</p>\r\n\t\t\t\t\t\t\t\t\t</td>\r\n\t\t\t\t\t\t\t\t</tr>\r\n\t\t\t\t\t\t\t\t<tr>\r\n\t\t\t\t\t\t\t\t\t<td style=\"height:40px;\">&nbsp;</td>\r\n\t\t\t\t\t\t\t\t</tr>\r\n\t\t\t\t\t\t\t</table>\r\n\t\t\t\t\t\t</td>\r\n\t\t\t\t\t</tr>\r\n\t\t\t\t\t<tr>\r\n\t\t\t\t\t\t<td style=\"height:80px;\">&nbsp;</td>\r\n\t\t\t\t\t</tr>\r\n\t\t\t\t</table>\r\n\t\t\t</td>\r\n\t\t</tr>\r\n\t</table>\r\n</body>\r\n</html>\r\n"

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

        public async Task<string> SubstituirCodigoNoHtml(string codigo)
        {
            string html = GetContent;
            if (string.IsNullOrWhiteSpace(codigo) || codigo.Length != 6)
                throw new ArgumentException("O código deve conter exatamente 6 dígitos.");

            for (int i = 0; i < 6; i++)
            {
                string placeholder = $"cod{i + 1}";
                html = html.Replace(placeholder, codigo[i].ToString());
            }

            return html;
        }
    }
}
