using Microsoft.AspNetCore.Mvc;

namespace LibraryApp.Controllers
{
	public class AccountController : Controller
	{
		public IActionResult Index()
		{
			return View();
		}
	}
}
