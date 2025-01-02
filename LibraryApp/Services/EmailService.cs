using System.Net.Mail;
using System.Net;
using LibraryApp.Models.ViewModel;

namespace LibraryApp.Services
{
    public class EmailService
    {

		private readonly IConfiguration _configuration;
      //  string EmailSettings;
        private string EmailAddress; // כתובת הדוא"ל שממנה תשלח ההודעה
        private string EmailPassword; // הסיסמה לדוא"ל (או סיסמת אפליקציה)
		public EmailService(IConfiguration configuration)
		{
			_configuration = configuration;

            EmailAddress = configuration.GetSection("EmailSettings")["EmailAddress"];
			EmailPassword= configuration.GetSection("EmailSettings")["EmailPassword"];

		}

		private const string SmtpServer = "smtp.gmail.com"; // כתובת שרת ה-SMTP
        private const int SmtpPort = 587; // פורט לשליחת מיילים
       

        public void SendEmail(string toAddress, string subject, string body)
        {
            try
            {
                // הגדרת הודעת המייל
                MailMessage mail = new MailMessage
                {
                    From = new MailAddress(EmailAddress),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true // אם רוצים שתוכן המייל יתמוך ב-HTML
                };

                mail.To.Add(toAddress);

                // הגדרת ה-SMTP
                SmtpClient smtp = new SmtpClient(SmtpServer, SmtpPort)
                {
                    Credentials = new NetworkCredential(EmailAddress, EmailPassword),
                    EnableSsl = true // חובה לשימוש ב-Gmail
                };

                // שליחת המייל
                smtp.Send(mail);
                Console.WriteLine("Email sent successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
            }
        }
    }

}
