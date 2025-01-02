using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using static LibraryApp.Models.ConnectionToDBmodel;

namespace LibraryApp.Controllers
{
    public class HomePageController : Controller
    {
        private readonly IConfiguration _configuration; 
        private readonly string connectionString;

        public HomePageController(IConfiguration configuration)
        {
            _configuration = configuration;
            connectionString = configuration.GetConnectionString("myConnect");
        }

        public IActionResult HomePage()
        {
            return View();
        }

        public IActionResult SignUp()
        {
            User newUser = new User();
            return View("SignUp", newUser);
        }

        public IActionResult SubmitUser(User user)
        {
            if (ModelState.IsValid)
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string sqlQuery = "INSERT INTO Users_tbl (Status, FirstName, LastName, Email, Password) VALUES (@Status, @FirstName, @LastName, @Email, @Password)";

                    using (var command = new SqlCommand(sqlQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Status", user.Status == "Admin" ? "PendingAdminApproval" : "User");
                        command.Parameters.AddWithValue("@FirstName", user.FirstName);
                        command.Parameters.AddWithValue("@LastName", user.LastName);
                        command.Parameters.AddWithValue("@Email", user.email);
                        command.Parameters.AddWithValue("@Password", user.Password);

                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            if (user.Status == "PendingAdminApproval")
                            {
                                TempData["SuccessMessage"] = "Admin request submitted successfully!";
                            }
                            return RedirectToAction("SignIn");
                        }


						Gmail gmail = new Gmail();



                        gmail.To = user.email;
						gmail.Subject = "Welcome to ebooklibraryservice – Account Successfully Created!";
						gmail.Body = "Dear " + user.FirstName + "\r\n\r\n"+ "Thank you for signing up! We’re excited to have you join our community.get started"+"\r\n\r\n"+ "If you have any questions, feel free to reach out to us at I.K.eBookLibraryService@gmail.com "+ "\r\n\r\n" + "We’re thrilled to have you on board!"
                            + "\r\n\r\n" + "Warm regards," + "\r\n\r\n" + " The I.K eBook Library Team";

						gmail.SendEmail();
					}
                }
            }

            return View("SignUp", user);
        }

        public IActionResult SignIn()
        {
            TempData["ErrorMessage"] = null;
            return View(new LogInModel());
        }

        [HttpPost]
        public IActionResult Login(LogInModel model)
        {
            if (ModelState.IsValid)
            {
                string status = IsUserValid(model.email, model.Password);

                if (status == "NonExistent")
                {
                    TempData["ErrorMessage"] = "No account exists with the provided email. Please check your email or sign up for an account.";
                    return View("SignIn", model);
                }

                if (status == "PendingAdminApproval")
                {
                    TempData["ErrorMessage"] = "Your admin request is still pending approval.";
                    return View("SignIn", model);
                }

                if (status == "Admin")
                {
                    HttpContext.Session.SetString("CurrentUser", model.email);
                    HttpContext.Session.SetString("Current user name", GetUser(model.email).FirstName);
                    return RedirectToAction("ShowAdminPage", "Admin");
                }

                if (status == "User")
                {
                    HttpContext.Session.SetString("CurrentUser", model.email);
                    HttpContext.Session.SetString("Current user name", GetUser(model.email).FirstName);
                    return RedirectToAction("ShowBooks");
                }
            }

            TempData["ErrorMessage"] = "Invalid email or password.";
            return View("SignIn", model);
        }




        private User GetUser(string email)
        {
            string query = "SELECT * FROM Users_tbl WHERE email = @email";

            var parameters = new Dictionary<string, object>
                {
                    { "@email", email }      
                };

            // יצירת חיבור למסד הנתונים
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // שליפת הספר מהמסד
            var user = connection.ExecuteQuery<User>(
                query, parameters, reader => new User
                {
                   FirstName = reader.GetString(1),
                   LastName = reader.GetString(2),
                }
            );


            return user.FirstOrDefault();
        }



        public IActionResult ShowBooks(string searchQuery)
        {
            List<Book> books = new List<Book>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = string.IsNullOrEmpty(searchQuery)
                    ? "SELECT * FROM Books"
                    : "SELECT * FROM Books WHERE LOWER(BookTitle) LIKE @SearchQuery";

                using (var command = new SqlCommand(query, connection))
                {
                    if (!string.IsNullOrEmpty(searchQuery))
                    {
                        command.Parameters.AddWithValue("@SearchQuery", $"%{searchQuery.ToLower()}%");
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            books.Add(new Book
                            {
                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                PriceForBorrow = reader.GetDecimal(7),
                                PriceForBuy = reader.GetDecimal(8),
                                AgeRestriction = reader.GetString(9),
                                IsOnSale = reader.GetBoolean(10),
                               
                                PDF = reader.GetBoolean(14),
                                epub = reader.GetBoolean(15),
                                f2b = reader.GetBoolean(16),
                                mobi = reader.GetBoolean(17),
                                Popularity = reader.GetInt32(18),
                                ImageUrl = reader.GetString(19),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)

                            });
                        }
                    }
                }
            }

            ViewBag.UserName = HttpContext.Session.GetString("Current user name");
           
            ViewBag.SearchQuery = searchQuery;

            return View("UserPageUpdated", books);
        }

        private string IsUserValid(string email, string password)
        {
            string query = "SELECT Status FROM Users_tbl WHERE email = @Email AND Password = @Password";

            using (var connection = new SqlConnection(connectionString))
            {
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Email", email);
                    command.Parameters.AddWithValue("@Password", password);

                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader["Status"].ToString();
                        }
                    }
                }
            }

            return "NonExistent";
        }

        public IActionResult BookDetails(string bookTitle, string author, string publisher, int yearOfPublication)
        {
            Book book = null;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // שאילתה לשימוש במפתח הראשי המשולב
                string query = @"
            SELECT * 
            FROM Books 
            WHERE BookTitle = @BookTitle 
              AND Author = @Author 
              AND Publisher = @Publisher 
              AND YearOfPublication = @YearOfPublication";

                using (var command = new SqlCommand(query, connection))
                {
                    // הוספת הפרמטרים
                    command.Parameters.AddWithValue("@BookTitle", bookTitle);
                    command.Parameters.AddWithValue("@Author", author);
                    command.Parameters.AddWithValue("@Publisher", publisher);
                    command.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            book = new Book
                            {

                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                PriceForBorrow = reader.GetDecimal(7),
                                PriceForBuy = reader.GetDecimal(8),
                                AgeRestriction = reader.GetString(9),
                                IsOnSale = reader.GetBoolean(10),

                                PDF = reader.GetBoolean(14),
                                epub = reader.GetBoolean(15),
                                f2b = reader.GetBoolean(16),
                                mobi = reader.GetBoolean(17),
                                Popularity = reader.GetInt32(18),
                                ImageUrl = reader.GetString(19),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)
                            };
                        }
                    }
                }
            }

            if (book == null)
            {
                return NotFound(); // אם הספר לא נמצא
            }

            return View(book);
        }

        [HttpGet]
        public IActionResult SearchBooks(string searchQuery)
        {
            List<Book> books = new List<Book>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
            SELECT * 
            FROM Books 
            WHERE 
                LOWER(BookTitle) LIKE LOWER(@SearchQuery) OR 
                LOWER(Author) LIKE LOWER(@SearchQuery) OR 
                LOWER(Publisher) LIKE LOWER(@SearchQuery)";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SearchQuery", $"%{searchQuery}%");

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            books.Add(new Book
                            {

                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                PriceForBorrow = reader.GetDecimal(7),
                                PriceForBuy = reader.GetDecimal(8),
                                AgeRestriction = reader.GetString(9),
                                IsOnSale = reader.GetBoolean(10),

                                PDF = reader.GetBoolean(14),
                                epub = reader.GetBoolean(15),
                                f2b = reader.GetBoolean(16),
                                mobi = reader.GetBoolean(17),
                                Popularity = reader.GetInt32(18),
                                ImageUrl = reader.GetString(19),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)

							});
                        }
                    }
                }
            }

            ViewBag.UserName = HttpContext.Session.GetString("CurrentUser");

            ViewBag.SearchQuery = searchQuery;

            return View("UserPageUpdated", books);
        }



        public IActionResult SortBooks(string sortOption)
        {
            List<Book> books = new List<Book>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Books";

                // הוספת מיון בהתאם לאופציה שנבחרה
                switch (sortOption)
                {
                    case "priceLowToHigh":
                        query += " ORDER BY PriceForBuy ASC";
                        break;
                    case "priceHighToLow":
                        query += " ORDER BY PriceForBuy DESC";
                        break;
                    case "popularity":
                        query += " ORDER BY Popularity DESC";
                        break;
                    case "genre":
                        query += " ORDER BY Genre ASC";
                        break;
                    case "year":
                        query += " ORDER BY YearOfPublication DESC";
                        break;
                }

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            books.Add(new Book
                            {

                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                PriceForBorrow = reader.GetDecimal(7),
                                PriceForBuy = reader.GetDecimal(8),
                                AgeRestriction = reader.GetString(9),
                                IsOnSale = reader.GetBoolean(10),

                                PDF = reader.GetBoolean(14),
                                epub = reader.GetBoolean(15),
                                f2b = reader.GetBoolean(16),
                                mobi = reader.GetBoolean(17),
                                Popularity = reader.GetInt32(18),
                                ImageUrl = reader.GetString(19),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)
                            });
                        }
                    }
                }
            }

            ViewBag.UserName = HttpContext.Session.GetString("CurrentUser");
            ViewBag.SortOption = sortOption;

            return View("UserPageUpdated", books);
        }



        public IActionResult FilterBooks(string author, string genre, decimal? minPrice, decimal? maxPrice, string onSale, string availability)
        {
            List<Book> books = new List<Book>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Books WHERE 1=1";

                // Add filters
                if (!string.IsNullOrEmpty(author))
                {
                    query += " AND LOWER(Author) LIKE LOWER(@Author)";
                }
                if (!string.IsNullOrEmpty(genre))
                {
                    query += " AND LOWER(Genre) LIKE LOWER(@Genre)";
                }
                if (minPrice.HasValue)
                {
                    query += " AND PriceForBuy >= @MinPrice";
                }
                if (maxPrice.HasValue)
                {
                    query += " AND PriceForBuy <= @MaxPrice";
                }
                if (!string.IsNullOrEmpty(onSale))
                {
                    query += " AND IsOnSale = @OnSale";
                }
                if (!string.IsNullOrEmpty(availability))
                {
                    if (availability == "borrow")
                    {
                        query += " AND PriceForBorrow > 0";
                    }
                    else if (availability == "buy")
                    {
                        query += " AND PriceForBuy > 0";
                    }
                }

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Add parameters
                    if (!string.IsNullOrEmpty(author))
                    {
                        command.Parameters.AddWithValue("@Author", $"%{author}%");
                    }
                    if (!string.IsNullOrEmpty(genre))
                    {
                        command.Parameters.AddWithValue("@Genre", $"%{genre}%");
                    }
                    if (minPrice.HasValue)
                    {
                        command.Parameters.AddWithValue("@MinPrice", minPrice.Value);
                    }
                    if (maxPrice.HasValue)
                    {
                        command.Parameters.AddWithValue("@MaxPrice", maxPrice.Value);
                    }
                    if (!string.IsNullOrEmpty(onSale))
                    {
                        command.Parameters.AddWithValue("@OnSale", onSale.ToLower() == "true");
                    }

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            books.Add(new Book
                            {

                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                PriceForBorrow = reader.GetDecimal(7),
                                PriceForBuy = reader.GetDecimal(8),
                                AgeRestriction = reader.GetString(9),
                                IsOnSale = reader.GetBoolean(10),

                                PDF = reader.GetBoolean(14),
                                epub = reader.GetBoolean(15),
                                f2b = reader.GetBoolean(16),
                                mobi = reader.GetBoolean(17),
                                Popularity = reader.GetInt32(18),
                                ImageUrl = reader.GetString(19),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)
                            });
                        }
                    }
                }
            }

            ViewBag.Author = author;
            ViewBag.Genre = genre;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.OnSale = onSale;
            ViewBag.Availability = availability;

            return View("UserPageUpdated", books);
        }




    }
}
