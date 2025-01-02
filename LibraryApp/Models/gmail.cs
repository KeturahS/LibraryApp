using System.Net.Mail;
using System.Net;

namespace LibraryApp.Models
{
    public class Gmail
    {
        public string To { get; set; }
        public string From { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }


		public void SendEmail()
		{
			MailMessage mm = new MailMessage("I.K.eBookLibraryService@gmail.com", To);
			mm.Subject = Subject;
			mm.Body = Body;
			mm.IsBodyHtml = false;

			SmtpClient smtp = new SmtpClient();
			smtp.Host = "smtp.gmail.com";
			smtp.Port = 587;
			smtp.EnableSsl = true;

			NetworkCredential nc = new NetworkCredential("I.K.eBookLibraryService@gmail.com", "upgu rdtj sasf irno");
			smtp.UseDefaultCredentials = false;
			smtp.Credentials = nc;
			smtp.Send(mm);
		

		}



	}
}
