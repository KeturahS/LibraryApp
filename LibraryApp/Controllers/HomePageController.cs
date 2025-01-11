using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using static LibraryApp.Models.ConnectionToDBmodel;
using Stripe.Terminal;
using System.Data;

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

                if (DoesEmailExist(user.email))
                {
                    ModelState.AddModelError("Email", "Email is already taken. Please use a different email.");
                    return View("SignUp", user);
                }


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



        private bool DoesEmailExist(string email)
        {
            // יצירת אובייקט של המחלקה שמחברת למסד הנתונים
            var dbHelper = new ConnectionToDBmodel.ConnectionToDBModel(_configuration);

            // שאילתת SQL לבדיקה אם האימייל כבר קיים
            string query = "SELECT COUNT(*) FROM Users_tbl WHERE Email = @Email";
            var parameters = new Dictionary<string, object>
    {
        { "@Email", email }
    };

            // החזרת תוצאה (true אם קיים, אחרת false)
            return dbHelper.ExecuteScalar<int>(query, parameters) > 0;
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


                UpdateIfBooksAreNoLongerOnSale();    ///////////עדכון המבצעים והגבלתם לכמות הימים שנבחרה לפני כניסת המשתמשים למערכת כדי שהכל יהיה מעודכן


				if (status == "Admin" || status=="SuperAdmin")
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




		private void UpdateIfBooksAreNoLongerOnSale()
		{
			// Query to update books where the sale period has ended
			string query = @"
        UPDATE Books 
        SET 
            IsOnSale = @IsOnSale,
            DISCOUNTEDPriceForBorrow = @DISCOUNTEDPriceForBorrow,
            DISCOUNTEDPriceForBuy = @DISCOUNTEDPriceForBuy,
            SaleStartDate = @DefaultDate,
            SaleEndDate = @DefaultDate,
            AmountOfSaleDays = @AmountOfSaleDays
        WHERE 
            IsOnSale = 1 AND SaleEndDate < GETDATE()";

			// Parameters to reset sale-related fields
			var parameters = new Dictionary<string, object>
	{
		{ "@IsOnSale", false }, // Set IsOnSale to false
        { "@DISCOUNTEDPriceForBorrow", -1 }, // Reset discounted price for borrow
        { "@DISCOUNTEDPriceForBuy", -1 }, // Reset discounted price for buy
        { "@DefaultDate", new DateTime(1753, 1, 1) }, // Default minimum date
        { "@AmountOfSaleDays", 0 } // Reset sale days to 0
    };

			// Create a connection to the database
			ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

			// Execute the query
			int rowsAffected = connection.ExecuteNonQuery(query, parameters);

			// Optional: Log or notify how many rows were updated
			Console.WriteLine($"{rowsAffected} book(s) updated: Sale information cleared.");
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
             
                }
            );


            return user.FirstOrDefault();
        }





        public IActionResult ShowBooks(string searchQuery)
        {
            // בדיקה האם המשתמש מחובר
            string userName = HttpContext.Session.GetString("CurrentUser");
            if (string.IsNullOrEmpty(userName))
            {
                TempData["ErrorMessage"] = "You must be logged in to view this page.";
                return RedirectToAction("SignIn", "HomePage");
            }
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
                                BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                                Author = reader.GetString(reader.GetOrdinal("Author")),
                                Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                                YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                                Genre = reader.GetString(reader.GetOrdinal("Genre")),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBorrow")),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBuy")),
                                PriceForBorrow = reader.GetDecimal(reader.GetOrdinal("PriceForBorrow")),
                                PriceForBuy = reader.GetDecimal(reader.GetOrdinal("PriceForBuy")),
                                AgeRestriction = reader.GetString(reader.GetOrdinal("AgeRestriction")),
                                IsOnSale = reader.GetBoolean(reader.GetOrdinal("IsOnSale")),
                                AmountOfSaleDays = reader.GetInt32(reader.GetOrdinal("AmountOfSaleDays")),
                                SaleStartDate = reader.GetDateTime(reader.GetOrdinal("SaleStartDate")),
                                SaleEndDate = reader.GetDateTime(reader.GetOrdinal("SaleEndDate")),
                                PDF = reader.GetBoolean(reader.GetOrdinal("PDF")),
                                epub = reader.GetBoolean(reader.GetOrdinal("epub")),
                                f2b = reader.GetBoolean(reader.GetOrdinal("f2b")),
                                mobi = reader.GetBoolean(reader.GetOrdinal("mobi")),
                                Popularity = reader.GetInt32(reader.GetOrdinal("Popularity")),
                                ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl")),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(reader.GetOrdinal("AvailableAmountOfCopiesToBorrow")),
                                BuyOnly = reader.GetBoolean(reader.GetOrdinal("BuyOnly"))
                            });
                        }
                    }
                }
            }

            // טוען את רשימות הסופרים והז'אנרים
            ViewBag.AllAuthors = GetAllAuthorsFromDatabase();
            ViewBag.AllGenres = GetAllGenresFromDatabase();

            ViewBag.UserName = userName;
           
            ViewBag.SearchQuery = searchQuery;

            string firstName = GetUserFirstName(userName);

            ViewBag.FirstName=firstName;

            int bookCount = AmountOfBooksInDB();
            ViewBag.BookCount = bookCount;

            return View("UserPageUpdated", books);
        }



        public int AmountOfBooksInDB()
        {
            
            var dbHelper = new ConnectionToDBmodel.ConnectionToDBModel(_configuration);

            
            string query = "SELECT COUNT(*) FROM Books";
            var totalBooks = dbHelper.ExecuteScalar<int>(query, new Dictionary<string, object>());
         

            return totalBooks;


        }




        private string GetUserFirstName(string email)
        {
            // Create an instance of the database helper
            var dbHelper = new ConnectionToDBmodel.ConnectionToDBModel(_configuration);

            // Define the SQL query
            string query = "SELECT FirstName FROM Users_tbl WHERE Email = @Email";

            // Define the parameters for the query
            var parameters = new Dictionary<string, object>
    {
        { "@Email", email }
    };

            // Execute the query and return the result
            return dbHelper.ExecuteScalar<string>(query, parameters);
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
            List<Feedback> feedbacks = new List<Feedback>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // שאילתה לשליפת פרטי הספר
                string bookQuery = @"
        SELECT * 
        FROM Books 
        WHERE BookTitle = @BookTitle 
          AND Author = @Author 
          AND Publisher = @Publisher 
          AND YearOfPublication = @YearOfPublication";

                using (var command = new SqlCommand(bookQuery, connection))
                {
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
                                BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                                Author = reader.GetString(reader.GetOrdinal("Author")),
                                Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                                YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                                Genre = reader.GetString(reader.GetOrdinal("Genre")),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBorrow")),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBuy")),
                                PriceForBorrow = reader.GetDecimal(reader.GetOrdinal("PriceForBorrow")),
                                PriceForBuy = reader.GetDecimal(reader.GetOrdinal("PriceForBuy")),
                                AgeRestriction = reader.GetString(reader.GetOrdinal("AgeRestriction")),
                                IsOnSale = reader.GetBoolean(reader.GetOrdinal("IsOnSale")),
                                AmountOfSaleDays = reader.GetInt32(reader.GetOrdinal("AmountOfSaleDays")),
                                SaleStartDate = reader.GetDateTime(reader.GetOrdinal("SaleStartDate")),
                                SaleEndDate = reader.GetDateTime(reader.GetOrdinal("SaleEndDate")),
                                PDF = reader.GetBoolean(reader.GetOrdinal("PDF")),
                                epub = reader.GetBoolean(reader.GetOrdinal("epub")),
                                f2b = reader.GetBoolean(reader.GetOrdinal("f2b")),
                                mobi = reader.GetBoolean(reader.GetOrdinal("mobi")),
                                Popularity = reader.GetInt32(reader.GetOrdinal("Popularity")),
                                ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl")),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(reader.GetOrdinal("AvailableAmountOfCopiesToBorrow")),
                                BuyOnly = reader.GetBoolean(reader.GetOrdinal("BuyOnly"))
                            };
                        }
                    }
                }

                if (book == null)
                {
                    return NotFound(); // אם הספר לא נמצא
                }

                // שאילתה לשליפת הפידבקים לספר
                string feedbackQuery = @"
    SELECT UserName, Rating, Feedback AS Comment, FeedbackDate 
FROM BookFeedback 
WHERE RTRIM(LTRIM(BookTitle)) = RTRIM(LTRIM(@BookTitle))
  AND RTRIM(LTRIM(Author)) = RTRIM(LTRIM(@Author))
  AND RTRIM(LTRIM(Publisher)) = RTRIM(LTRIM(@Publisher))
  AND YearOfPublication = @YearOfPublication;
";

                using (var feedbackCommand = new SqlCommand(feedbackQuery, connection))
                {
                    feedbackCommand.Parameters.AddWithValue("@BookTitle", bookTitle);
                    feedbackCommand.Parameters.AddWithValue("@Author", author);
                    feedbackCommand.Parameters.AddWithValue("@Publisher", publisher);
                    feedbackCommand.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                    using (var feedbackReader = feedbackCommand.ExecuteReader())
                    {
                        while (feedbackReader.Read())
                        {
                            feedbacks.Add(new Feedback
                            {
                                UserName = feedbackReader.GetString(feedbackReader.GetOrdinal("UserName")),
                                Rating = feedbackReader.GetInt32(feedbackReader.GetOrdinal("Rating")),
                                Comment = feedbackReader.GetString(feedbackReader.GetOrdinal("Comment")),
                                FeedbackDate = feedbackReader.GetDateTime(feedbackReader.GetOrdinal("FeedbackDate"))
                            });
                        }
                    }
                }
            }

          

            // העברת הספר והפידבקים ל-View
            ViewBag.Feedbacks = feedbacks;


            bool isBookOnWaitingList = IsBookOnWaitingList(book);


            if (isBookOnWaitingList)
            {
                TempData["Is book on borrowing waiting list"] = "yes";


            }
            else
            {
                TempData["Is book on borrowing waiting list"] = "no";
            }
            bool ans = IsUserOnWaitingList(book);


            if (ans)
            {
                TempData["Is current user on borrowing waiting list"] = "yes";

                bool ans2 = IsItUserTurnToBorrow(book);

                if (ans2)
                {
                    TempData["Is it current user's turn to borrow"] = "yes";
                }
                else
                    TempData["Is it current user's turn to borrow"] = "no";
            }
            else
            {
                TempData["Is current user on borrowing waiting list"] = "no";


                TempData["Is it current user's turn to borrow"] = "yes";
            }






            return View("BookDetails",book);
        }



        public bool IsItUserTurnToBorrow(Book book)
        {
            // Get the current user's email from the session
            string email = HttpContext.Session.GetString("CurrentUser");

            // Return false if the email is null or empty
            if (string.IsNullOrEmpty(email))
            {
                return false;
            }

            // SQL query to check the user's place in the queue
            string query = @"
    SELECT PlaceInQueue 
    FROM BorrowingBookWaitingList 
    WHERE email = @Email 
      AND BookTitle = @BookTitle 
      AND Author = @Author 
      AND Publisher = @Publisher 
      AND YearOfPublication = @YearOfPublication";

            // Set up parameters
            var parameters = new Dictionary<string, object>
    {
        { "@Email", email },
        { "@BookTitle", book.BookTitle },
        { "@Author", book.Author },
        { "@Publisher", book.Publisher },
        { "@YearOfPublication", book.YearOfPublication }
    };

            // Create a database connection
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Define a mapper function to extract PlaceInQueue from the SqlDataReader
            Func<SqlDataReader, int?> mapper = reader =>
            {
                // Check for null and return PlaceInQueue as a nullable integer
                return reader.IsDBNull(reader.GetOrdinal("PlaceInQueue"))
                    ? (int?)null
                    : reader.GetInt32(reader.GetOrdinal("PlaceInQueue"));
            };

            // Execute the query and get the result as a list of integers (or nullable integers)
            List<int?> results = connection.ExecuteQuery(query, parameters, mapper);

            // Get the first result (if any)
            int? placeInQueue = results.FirstOrDefault();

            // Check if the user is not in the waiting list
            if (!placeInQueue.HasValue)
            {
                return false;
            }

            // Return true if the user's position is 1, 2, or 3; otherwise, return false
            return placeInQueue.Value >= 1 && placeInQueue.Value <= 3;
        }



        public bool IsBookOnWaitingList(Book book)
        {
            string email = HttpContext.Session.GetString("CurrentUser");


            string query = "SELECT COUNT(1) FROM BorrowingBookWaitingList WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>
              {
                   { "@Email", email },
                   { "@BookTitle", book.BookTitle },
                   { "@Author", book.Author },
                   { "@Publisher", book.Publisher },
                   { "@YearOfPublication", book.YearOfPublication }
                };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);


            int res = connection.ExecuteScalar<int>(query, parameters);

            return res > 0;
        }

        public bool IsUserOnWaitingList(Book book)
        {
            string email = HttpContext.Session.GetString("CurrentUser");


            string query = "SELECT COUNT(1) FROM BorrowingBookWaitingList WHERE email= @Email AND BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>
              {
                   { "@Email", email },
                   { "@BookTitle", book.BookTitle },
                   { "@Author", book.Author },
                   { "@Publisher", book.Publisher },
                   { "@YearOfPublication", book.YearOfPublication }
                };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);


            int res = connection.ExecuteScalar<int>(query, parameters);

            return res > 0;
        }

        private Book GetBook(string BookTitle, string Author, string Publisher, int YearOfPublication)
        {
            string query = "SELECT * FROM Books WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication";

            var parameters = new Dictionary<string, object>
    {
        { "@BookTitle", BookTitle },
        { "@Author", Author },
        { "@Publisher", Publisher },
        { "@YearOfPublication", YearOfPublication }
    };

            // יצירת חיבור למסד הנתונים
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // שליפת הספר מהמסד
            var books = connection.ExecuteQuery<Book>(
                query, parameters, reader => new Book
                {
                    BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                    Author = reader.GetString(reader.GetOrdinal("Author")),
                    Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                    YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                    Genre = reader.GetString(reader.GetOrdinal("Genre")),
                    DISCOUNTEDPriceForBorrow = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBorrow")),
                    DISCOUNTEDPriceForBuy = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBuy")),
                    PriceForBorrow = reader.GetDecimal(reader.GetOrdinal("PriceForBorrow")),
                    PriceForBuy = reader.GetDecimal(reader.GetOrdinal("PriceForBuy")),
                    AgeRestriction = reader.GetString(reader.GetOrdinal("AgeRestriction")),
                    IsOnSale = reader.GetBoolean(reader.GetOrdinal("IsOnSale")),
                    AmountOfSaleDays = reader.GetInt32(reader.GetOrdinal("AmountOfSaleDays")),
                    SaleStartDate = reader.GetDateTime(reader.GetOrdinal("SaleStartDate")),
                    SaleEndDate = reader.GetDateTime(reader.GetOrdinal("SaleEndDate")),
                    PDF = reader.GetBoolean(reader.GetOrdinal("PDF")),
                    epub = reader.GetBoolean(reader.GetOrdinal("epub")),
                    f2b = reader.GetBoolean(reader.GetOrdinal("f2b")),
                    mobi = reader.GetBoolean(reader.GetOrdinal("mobi")),
                    Popularity = reader.GetInt32(reader.GetOrdinal("Popularity")),
                    ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl")),
                    AvailableAmountOfCopiesToBorrow = reader.GetInt32(reader.GetOrdinal("AvailableAmountOfCopiesToBorrow")),
                    BuyOnly = reader.GetBoolean(reader.GetOrdinal("BuyOnly"))


                }
            );


            return books.FirstOrDefault();
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

                                BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                                Author = reader.GetString(reader.GetOrdinal("Author")),
                                Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                                YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                                Genre = reader.GetString(reader.GetOrdinal("Genre")),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBorrow")),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBuy")),
                                PriceForBorrow = reader.GetDecimal(reader.GetOrdinal("PriceForBorrow")),
                                PriceForBuy = reader.GetDecimal(reader.GetOrdinal("PriceForBuy")),
                                AgeRestriction = reader.GetString(reader.GetOrdinal("AgeRestriction")),
                                IsOnSale = reader.GetBoolean(reader.GetOrdinal("IsOnSale")),
                                AmountOfSaleDays = reader.GetInt32(reader.GetOrdinal("AmountOfSaleDays")),
                                SaleStartDate = reader.GetDateTime(reader.GetOrdinal("SaleStartDate")),
                                SaleEndDate = reader.GetDateTime(reader.GetOrdinal("SaleEndDate")),
                                PDF = reader.GetBoolean(reader.GetOrdinal("PDF")),
                                epub = reader.GetBoolean(reader.GetOrdinal("epub")),
                                f2b = reader.GetBoolean(reader.GetOrdinal("f2b")),
                                mobi = reader.GetBoolean(reader.GetOrdinal("mobi")),
                                Popularity = reader.GetInt32(reader.GetOrdinal("Popularity")),
                                ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl")),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(reader.GetOrdinal("AvailableAmountOfCopiesToBorrow")),
                                BuyOnly = reader.GetBoolean(reader.GetOrdinal("BuyOnly"))

                            });
                        }
                    }
                }
            }

            ViewBag.UserName = HttpContext.Session.GetString("Current user name");

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

                                BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                                Author = reader.GetString(reader.GetOrdinal("Author")),
                                Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                                YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                                Genre = reader.GetString(reader.GetOrdinal("Genre")),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBorrow")),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBuy")),
                                PriceForBorrow = reader.GetDecimal(reader.GetOrdinal("PriceForBorrow")),
                                PriceForBuy = reader.GetDecimal(reader.GetOrdinal("PriceForBuy")),
                                AgeRestriction = reader.GetString(reader.GetOrdinal("AgeRestriction")),
                                IsOnSale = reader.GetBoolean(reader.GetOrdinal("IsOnSale")),
                                AmountOfSaleDays = reader.GetInt32(reader.GetOrdinal("AmountOfSaleDays")),
                                SaleStartDate = reader.GetDateTime(reader.GetOrdinal("SaleStartDate")),
                                SaleEndDate = reader.GetDateTime(reader.GetOrdinal("SaleEndDate")),
                                PDF = reader.GetBoolean(reader.GetOrdinal("PDF")),
                                epub = reader.GetBoolean(reader.GetOrdinal("epub")),
                                f2b = reader.GetBoolean(reader.GetOrdinal("f2b")),
                                mobi = reader.GetBoolean(reader.GetOrdinal("mobi")),
                                Popularity = reader.GetInt32(reader.GetOrdinal("Popularity")),
                                ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl")),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(reader.GetOrdinal("AvailableAmountOfCopiesToBorrow")),
                                BuyOnly = reader.GetBoolean(reader.GetOrdinal("BuyOnly"))

                            });
                        }
                    }
                }
            }

            ViewBag.UserName = HttpContext.Session.GetString("CurrentUser");
            ViewBag.SortOption = sortOption;

            return View("UserPageUpdated", books);
        }



        public IActionResult FilterBooks(List<string> authors, List<string> genres, decimal? minPrice, decimal? maxPrice, string onSale, string availability, List<string> method)
        {
            List<Book> books = new List<Book>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Books WHERE 1=1";

                // סינון לפי סופרים
                if (authors != null && authors.Count > 0)
                {
                    query += " AND Author IN (" + string.Join(",", authors.Select(a => $"'{a}'")) + ")";
                }

                // סינון לפי ז'אנרים
                if (genres != null && genres.Count > 0)
                {
                    query += " AND Genre IN (" + string.Join(",", genres.Select(g => $"'{g}'")) + ")";
                }

                // סינון לפי מחיר
                if (minPrice.HasValue)
                {
                    query += " AND PriceForBuy >= @MinPrice";
                }
                if (maxPrice.HasValue)
                {
                    query += " AND PriceForBuy <= @MaxPrice";
                }

                // סינון לפי הנחה
                if (!string.IsNullOrEmpty(onSale) && onSale == "true")
                {
                    query += " AND (DISCOUNTEDPriceForBuy > 0 OR DISCOUNTEDPriceForBorrow > 0)";
                }



                // סינון לפי שיטה (קנייה או השאלה)
                if (method != null && method.Any(m => !string.IsNullOrWhiteSpace(m)))
                {

                    List<string> methodConditions = new List<string>();

                    if (method.Contains("buy"))
                    {
                        methodConditions.Add("PriceForBuy > 0");
                    }
                    if (method.Contains("borrow"))
                    {
                        methodConditions.Add("PriceForBorrow > 0 AND AvailableAmountOfCopiesToBorrow > 0 AND BuyOnly = 0");
                    }

                    if (methodConditions.Count > 0)
                    {
                        query += " AND (" + string.Join(" OR ", methodConditions) + ")";
                    }
                }
                




                using (SqlCommand command = new SqlCommand(query, connection))
                {
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
                                    BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                                    Author = reader.GetString(reader.GetOrdinal("Author")),
                                    Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                                    YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                                    Genre = reader.GetString(reader.GetOrdinal("Genre")),
                                    DISCOUNTEDPriceForBorrow = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBorrow")),
                                    DISCOUNTEDPriceForBuy = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBuy")),
                                    PriceForBorrow = reader.GetDecimal(reader.GetOrdinal("PriceForBorrow")),
                                    PriceForBuy = reader.GetDecimal(reader.GetOrdinal("PriceForBuy")),
                                    AgeRestriction = reader.GetString(reader.GetOrdinal("AgeRestriction")),
                                    IsOnSale = reader.GetBoolean(reader.GetOrdinal("IsOnSale")),
                                    AmountOfSaleDays = reader.GetInt32(reader.GetOrdinal("AmountOfSaleDays")),
                                    SaleStartDate = reader.GetDateTime(reader.GetOrdinal("SaleStartDate")),
                                    SaleEndDate = reader.GetDateTime(reader.GetOrdinal("SaleEndDate")),
                                    PDF = reader.GetBoolean(reader.GetOrdinal("PDF")),
                                    epub = reader.GetBoolean(reader.GetOrdinal("epub")),
                                    f2b = reader.GetBoolean(reader.GetOrdinal("f2b")),
                                    mobi = reader.GetBoolean(reader.GetOrdinal("mobi")),
                                    Popularity = reader.GetInt32(reader.GetOrdinal("Popularity")),
                                    ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl")),
                                    AvailableAmountOfCopiesToBorrow = reader.GetInt32(reader.GetOrdinal("AvailableAmountOfCopiesToBorrow")),
                                    BuyOnly = reader.GetBoolean(reader.GetOrdinal("BuyOnly"))
                                });
                            }
                        
                       

                    }
                }
            }

            // טוען מחדש את כל הסופרים והז'אנרים
            ViewBag.AllAuthors = GetAllAuthorsFromDatabase();
            ViewBag.AllGenres = GetAllGenresFromDatabase();

            // מעביר את ערכי הסינון ל-ViewBag כדי לשמר את הבחירות של המשתמש
            ViewBag.Authors = authors;
            ViewBag.Genres = genres;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.OnSale = onSale;
            ViewBag.Availability = availability;
            ViewBag.Method = method;

            return View("UserPageUpdated", books);
        }


        private List<string> GetAllAuthorsFromDatabase()
        {
            List<string> authors = new List<string>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT DISTINCT Author FROM Books";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            authors.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return authors;
        }

        private List<string> GetAllGenresFromDatabase()
        {
            List<string> genres = new List<string>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT DISTINCT Genre FROM Books";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            genres.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return genres;
        }



        [HttpPost]
        public IActionResult AddServiceFeedback(int rating, string feedback)
        {

            string userName = HttpContext.Session.GetString("CurrentUser");

            if (string.IsNullOrEmpty(userName))
            {
                TempData["ErrorMessage"] = "You must be logged in to leave feedback.";
                return RedirectToAction("SignIn", "HomePage");
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
            INSERT INTO ServiceFeedback (UserName, Rating, Feedback, FeedbackDate) 
            VALUES (@UserName, @Rating, @Feedback, GETDATE())";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserName", userName);
                    command.Parameters.AddWithValue("@Rating", rating);
                    command.Parameters.AddWithValue("@Feedback", feedback ?? (object)DBNull.Value);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        TempData["SuccessMessage"] = "Thank you for your feedback!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to submit feedback. Please try again.";
                    }
                }
            }

            return RedirectToAction("ShowBooks", "HomePage");
        }



        public List<(string UserName, int Rating, string Feedback, DateTime FeedbackDate)> GetServiceFeedbacks()
        {
            List<(string, int, string, DateTime)> feedbacks = new List<(string, int, string, DateTime)>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT UserName, Rating, Feedback, FeedbackDate FROM ServiceFeedback ORDER BY FeedbackDate DESC";

                using (var command = new SqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        feedbacks.Add((
                            reader.GetString(0),     // UserName
                            reader.GetInt32(1),      // Rating
                            reader.IsDBNull(2) ? "" : reader.GetString(2),  // Feedback
                            reader.GetDateTime(3)    // FeedbackDate
                        ));
                    }
                }
            }

            return feedbacks;
        }


        // פעולה שתציג את טופס הפידבק והרייטינג
        public IActionResult RateUs()
        {
            // בדיקה האם המשתמש מחובר
            string userName = HttpContext.Session.GetString("CurrentUser");
            if (string.IsNullOrEmpty(userName))
            {
                TempData["ErrorMessage"] = "You must be logged in to view this page.";
                return RedirectToAction("SignIn", "HomePage");
            }
            return View("RateUs"); // יצירת View בשם "RateUs"
        }


        public IActionResult feedbacksAboutUs()
        {

            // שליפת הפידבקים מבסיס הנתונים
            var feedbacks = GetServiceFeedbacks();

            // העברת הפידבקים ל-ViewBag כדי להציג אותם ב-View
            ViewBag.ServiceFeedbacks = feedbacks;

            return View("feedbacksAboutUs"); 
        }




    }
}
