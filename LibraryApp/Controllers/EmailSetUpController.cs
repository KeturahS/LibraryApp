using Microsoft.AspNetCore.Mvc;

using LibraryApp.Models;

using System.Net;
using System.Net.Mail;

namespace LibraryApp.Controllers
{
    public class EmailSetUpController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Index(LibraryApp.Models.gmail model)
        {
            MailMessage mm= new MailMessage("I.K.eBookLibraryService@gmail.com", model.To);
            mm.Subject = model.Subject;
            mm.Body = model.Body;
            mm.IsBodyHtml = false;

            SmtpClient smtp =new SmtpClient();
            smtp.Host = "smtp.gmail.com";
            smtp.Port = 587;
            smtp.EnableSsl = true;

            NetworkCredential nc = new NetworkCredential("I.K.eBookLibraryService@gmail.com", "upgu rdtj sasf irno");
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = nc;
            smtp.Send(mm);
            ViewBag.Message = "Mail has been sent successfuly";
            return View();
        }
    }
}
