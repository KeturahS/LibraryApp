﻿using Microsoft.AspNetCore.Mvc;

namespace LibraryApp.Controllers
{
    public class BookController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
