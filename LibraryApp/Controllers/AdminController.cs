using LibraryApp.Models;
using LibraryApp.Models.ViewModel;
using LibraryApp.Services;
using LibraryApp.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Policy;
using static LibraryApp.Models.ConnectionToDBmodel;
using PaypalServerSdk.Standard.Models;
using System.Data.Common;
using Stripe.Terminal;

namespace LibraryApp.Controllers
{
    public class AdminController : Controller
    {

        private readonly IConfiguration _configuration;
        string connectionString;
        public AdminViewModel AdminPageModel;
        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
            connectionString = configuration.GetConnectionString("myConnect");
            AdminPageModel = new AdminViewModel();



        }


        public IActionResult ShowAdminPage()
        {

            SetAdminPage();


            return View("Admin_Home_Page", AdminPageModel);
        }

        public void SetAdminPage()
        {
            AdminPageModel.AdminUserRequests = new List<User>();
            AdminPageModel.AdminUserRequests = GetPendingAdminRequests();
            AdminPageModel.AllUsers = new List<User>();
            AdminPageModel.AllUsers = GetAllUsers();
            AdminPageModel.NewUser = new User();
            // AdminPageModel.AllBooks = new List<Book>();
            // AdminPageModel.AllBooks = GetAllBooks();

        }






        public List<User> GetPendingAdminRequests()
        {

            List<User> PendingAdminRequests = new List<User>();

            string query = "SELECT * FROM Users_tbl WHERE Status = 'PendingAdminApproval'"; // Simple SQL query

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open(); // Open connection
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader()) // Execute query
                    {
                        while (reader.Read()) // Read the data
                        {
                            var user = new User
                            {
                                FirstName = reader.GetString(1),
                                LastName = reader.GetString(2),
                                email = reader.GetString(3),
                                // Add other fields as necessary
                            };

                            PendingAdminRequests.Add(user);
                        }
                    }
                }
            }

            return PendingAdminRequests; // Return the list of pending users
        }


        [HttpPost]
        public IActionResult ApproveRejectDeleteAdd(string userId, string action)
        {
        

            if (action == "approve")
            {

                var updateQuery = "UPDATE Users_tbl SET Status = @NewStatus WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                     { "@NewStatus", "Admin" },
                     { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteNonQuery(updateQuery, parameters);

                Gmail gmail = new Gmail();

                gmail.To = userId;
                gmail.Subject = "Approved admin request";
                gmail.Body = "Dear " + userId + ", we are glad to inform you about being approved as admin, welcome to the team!";

                gmail.SendEmail();


            }

            if (action == "reject")
            {
                var updateQuery = "UPDATE Users_tbl SET Status = @NewStatus WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                     { "@NewStatus", "Rejected" },
                     { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteNonQuery(updateQuery, parameters);

				Gmail gmail = new Gmail();

				gmail.To = userId;
				gmail.Subject = "Approved admin request";
				gmail.Body = "Dear " + userId + ", unfortunately, we must inform you about being rejected as admin, you are welcome to try to sign up as a regular user instead.";

				gmail.SendEmail();


                action = "Delete";

			}


            if (action == "Delete")
            {
                var updateQuery = "DELETE FROM Users_tbl WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                        { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteNonQuery(updateQuery, parameters);


            }



            return RedirectToAction("ShowAdminPage");
        }






        public List<User> GetAllUsers()
        {
            // SQL query to get all users except the current one
            var updateQuery = "SELECT * FROM Users_tbl WHERE email <> @CurrentUser";

            // Set up the parameters
            var parameters = new Dictionary<string, object>
            {
                { "@CurrentUser", HttpContext.Session.GetString("CurrentUser") }
            };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Execute the query and map the results to a list of User objects
            return connection.ExecuteQuery<User>(
                updateQuery,
                parameters,
                reader => new User
                {
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.GetString(reader.GetOrdinal("LastName")),
                    email = reader.GetString(reader.GetOrdinal("email")),
                    Password = reader.GetString(reader.GetOrdinal("Password"))
                }
            );

        }



        

        public IActionResult SendEmptyBook()
        {
            TempData["Message"] = null;
            Book book= new Book();
            book.BuyOnly = false;

            return View("AddBooks", book);
        }


       


        public IActionResult AddBook(Book book, bool BuyOnly)
        {

            string query = "SELECT COUNT(1) FROM Books WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>
              {
                   { "@BookTitle", book.BookTitle },
                   { "@Author",  book.Author },
                   { "@Publisher",  book.Publisher },
                   { "@YearOfPublication",  book.YearOfPublication }
                };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            var result = connection.ExecuteQuery<int>(query, parameters, reader => reader.GetInt32(0)).FirstOrDefault();

            if (result == 1)
            {
                TempData["Message"] = "Book already exists in the eBook Library";


                return View("AddBooks", book);

            }


            if (book.PriceForBorrow > book.PriceForBuy)
            {
                TempData["Message"] = "Borrowing price must be lower than purchase price";


                return View("AddBooks", book);
            }

            if (book.DISCOUNTEDPriceForBorrow > book.PriceForBorrow)
            {
                TempData["Message"] = "Discounted price for borrowing must be smaller than regular price for borrowing";


                return View("AddBooks", book);
            }

            if (book.DISCOUNTEDPriceForBorrow < 0)
            {
                TempData["Message"] = "Discounted price must positive";


                return View("AddBooks", book);

            }


            if (book.DISCOUNTEDPriceForBuy > book.PriceForBuy)
            {
                TempData["Message"] = "Discounted price for buying must be smaller than regular purchase price";


                return View("AddBooks", book);
            }

            if (book.DISCOUNTEDPriceForBuy < 0)
            {
                TempData["Message"] = "Discounted price must not be negetive";


                return View("AddBooks", book);

            }

                        


            if (book.DISCOUNTEDPriceForBorrow < book.PriceForBorrow && book.DISCOUNTEDPriceForBorrow > 0)
            {
                book.IsOnSale = true;

            }

            if (book.DISCOUNTEDPriceForBuy < book.PriceForBuy && book.DISCOUNTEDPriceForBuy > 0)
            {
                book.IsOnSale = true;

            }

  

            var updateQuery = @"
    INSERT INTO Books (
        BookTitle,
        Author,
        Publisher,
        YearOfPublication,
        Genre,
        DISCOUNTEDPriceForBorrow,
        DISCOUNTEDPriceForBuy,
        PriceForBorrow,
        PriceForBuy,
        AgeRestriction,
        IsOnSale,
        AmountOfSaleDays,  
        PDF,
        epub,
        f2b,
        mobi,
        Popularity,
        ImageUrl,
        AvailableAmountOfCopiesToBorrow,
        BuyOnly
        
    )
    VALUES (
        @BookTitle,
        @Author,
        @Publisher,
        @YearOfPublication,
        @Genre,
        @DISCOUNTEDPriceForBorrow,
        @DISCOUNTEDPriceForBuy,
        @PriceForBorrow,
        @PriceForBuy,
        @AgeRestriction,
        @IsOnSale,
        @AmountOfSaleDays,
        @PDF,
        @epub,
        @f2b,
        @mobi,
        @Popularity,
        @ImageUrl,
        @AvailableAmountOfCopiesToBorrow,
        @BuyOnly
    );";


			
            // Set up the parameters

            var parameters2 = new Dictionary<string, object>
                {
                   
                    { "@BookTitle", book.BookTitle },
                    { "@Author",  book.Author },
                    { "@Publisher",  book.Publisher },
                    { "@YearOfPublication",  book.YearOfPublication },
                    { "@Genre",  book.Genre },
                    { "@DISCOUNTEDPriceForBorrow", book.DISCOUNTEDPriceForBorrow },
                    { "@DISCOUNTEDPriceForBuy",book.DISCOUNTEDPriceForBuy },
                    { "@PriceForBorrow", book.PriceForBorrow },
                    { "@PriceForBuy",  book.PriceForBuy },
                    { "@AgeRestriction",  book.AgeRestriction },
                    { "@IsOnSale",  book.IsOnSale },
                    { "@AmountOfSaleDays",  book.AmountOfSaleDays },
                    { "@PDF", book.PDF },
                    { "@epub", book.epub },
                    { "@f2b", book.f2b },
                    { "@mobi", book.mobi },
                    { "@Popularity",  book.Popularity },
                    { "@ImageUrl",  book.ImageUrl },
                    { "@AvailableAmountOfCopiesToBorrow",  3 },
                    { "@BuyOnly", BuyOnly }
                   					
				};


            // Execute the query to insert the book
            connection.ExecuteNonQuery(
                   updateQuery,
                   parameters2
               );


			if (book.AmountOfSaleDays>0 && book.IsOnSale==true)
			{
				book.SaleStartDate = DateTime.Now;
				book.SaleEndDate = DateTime.Now.AddDays(book.AmountOfSaleDays);

				string query3 = "UPDATE Books SET SaleStartDate=@SaleStartDate, SaleEndDate=@SaleEndDate WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";
				var parameters3 = new Dictionary<string, object>
				{
					{ "@SaleStartDate", book.SaleStartDate },
					{ "@SaleEndDate",  book.SaleEndDate },
                     { "@BookTitle", book.BookTitle },
                    { "@Author",  book.Author },
                    { "@Publisher",  book.Publisher },
                    { "@YearOfPublication",  book.YearOfPublication }


                };

				connection.ExecuteNonQuery(
				  query3,
				  parameters3
			  );

			}


			TempData["BookAddedSuccessfully"] = "The Book " + book.BookTitle + " you submitted has been added successfuly";



            return View("AddBooks", new Book());


        }




        public IActionResult SelectBook(string BookChosen)  /// מחזיר את כל הספרים שנבחרו שיש להם את התחילית שהוזנה בתיבה החיפוש
        {

            string query = "SELECT * FROM Books WHERE BookTitle LIKE @prefix OR Author LIKE @prefix OR Publisher LIKE @prefix";
            var parameters = new Dictionary<string, object>
            {
                { "@prefix", BookChosen + "%" }
            };


            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Execute the query and map the results to a list of User objects

            AdminPageModel.SelectedBooks = new List<Book>();

            AdminPageModel.SelectedBooks =
            connection.ExecuteQuery<Book>(
             query, parameters, reader => new Book
             {
				 // Map database columns to Book properties
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


            SetAdminPage();


            return View("Admin_Home_Page", AdminPageModel);

        }



        public IActionResult ActionsForSelectedBook(string BookTitle, string Author, string Publisher, int YearOfPublication)
        {

            Book book = GetBook(BookTitle, Author, Publisher, YearOfPublication);

            if (book == null)
            {
                // אם לא נמצא ספר, החזרת View מתאים או הודעת שגיאה
                return View("Error", "The requested book was not found.");
            }

            // החזרת View עם הספר
            return View(book);


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


        public IActionResult UpdateBook(string BookTitle, string Author, string Publisher, int YearOfPublication, decimal PriceForBorrow, decimal PriceForBuy, decimal DISCOUNTEDPriceForBorrow, decimal DISCOUNTEDPriceForBuy, bool PDF, bool epub, bool f2b, bool mobi, string AgeRestriction, bool IsOnSale, int AmountOfSaleDays, int Popularity, bool BuyOnly)
            {
            Book book = GetBook(BookTitle, Author, Publisher, YearOfPublication);
            bool OnSALE = false;

            if (PriceForBorrow > PriceForBuy || DISCOUNTEDPriceForBorrow > PriceForBuy)
            {

                TempData["Message"] = "Price of borrowing must be less than price of purchasing";
                return View("ActionsForSelectedBook", book);

            }


            if (DISCOUNTEDPriceForBorrow > PriceForBorrow)
            {
                TempData["Message"] = "Discounted price for borrowing must be smaller than regular price for borrowing";
                return View("ActionsForSelectedBook", book);
            }


            if (DISCOUNTEDPriceForBuy > PriceForBuy)
            {
                TempData["Message"] = "Discounted price for buying must be smaller than regular purchase price";
                return View("ActionsForSelectedBook", book);
            }



            if (DISCOUNTEDPriceForBorrow < PriceForBorrow && DISCOUNTEDPriceForBorrow > 0)
            {
                OnSALE = true;

            }

            if (DISCOUNTEDPriceForBuy < PriceForBuy && DISCOUNTEDPriceForBuy > 0)
            {
                OnSALE = true;

            }

            if (DISCOUNTEDPriceForBorrow <= 0)
            {
                DISCOUNTEDPriceForBorrow = book.DISCOUNTEDPriceForBorrow;

                if (DISCOUNTEDPriceForBorrow < PriceForBorrow && DISCOUNTEDPriceForBorrow > 0)
                {
                    OnSALE = true;



                }

            }

            if (DISCOUNTEDPriceForBuy <= 0)
            {
                DISCOUNTEDPriceForBuy = book.DISCOUNTEDPriceForBuy;
                if (DISCOUNTEDPriceForBuy < PriceForBuy && DISCOUNTEDPriceForBuy > 0)
                {
                    OnSALE = true;

                }
            }




            string query = "UPDATE Books SET PriceForBorrow = @PriceForBorrow, PriceForBuy = @PriceForBuy, DISCOUNTEDPriceForBorrow= @DISCOUNTEDPriceForBorrow, DISCOUNTEDPriceForBuy=@DISCOUNTEDPriceForBuy, IsOnSale = @IsOnSale, PDF = @PDF, epub = @epub, f2b = @f2b, mobi= @mobi, AgeRestriction=@AgeRestriction, AmountOfSaleDays=@AmountOfSaleDays, Popularity=@Popularity WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>
              {

					{ "@BookTitle", BookTitle },
					{ "@Author",  Author },
					{ "@Publisher", Publisher },
					{ "@YearOfPublication", YearOfPublication },
					{ "@DISCOUNTEDPriceForBorrow", DISCOUNTEDPriceForBorrow },
					{ "@DISCOUNTEDPriceForBuy",DISCOUNTEDPriceForBuy },
					{ "@PriceForBorrow", PriceForBorrow },
					{ "@PriceForBuy",  PriceForBuy },
					{ "@AgeRestriction",  AgeRestriction },
					{ "@IsOnSale",  IsOnSale },
					{ "@AmountOfSaleDays",  AmountOfSaleDays },
					{ "@PDF", PDF },
					{ "@epub", epub },
					{ "@f2b", f2b },
					{ "@mobi", mobi },
					{ "@Popularity",  Popularity },
					{ "@BuyOnly", BuyOnly }
				};

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Execute the query and map the results to a list of User objects



            connection.ExecuteNonQuery(
             query, parameters);


            Book book1 = GetBook(BookTitle, Author, Publisher, YearOfPublication);



            return View("ActionsForSelectedBook", book1);


        }

        public IActionResult DeleteSale(string BookTitle, string Author, string Publisher, int YearOfPublication)
        {

            string query = "UPDATE Books SET IsOnSale = @IsOnSale, DISCOUNTEDPriceForBorrow= @DISCOUNTEDPriceForBorrow,  DISCOUNTEDPriceForBuy = @DISCOUNTEDPriceForBuy WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>
              {
                   { "@BookTitle", BookTitle },
                   { "@Author",  Author },
                   { "@Publisher",  Publisher },
                   { "@YearOfPublication",  YearOfPublication },
                   { "@DISCOUNTEDPriceForBorrow", -1 },
                   { "@DISCOUNTEDPriceForBuy",-1 },
                   { "@IsOnSale",  false },

                };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Execute the query and map the results to a list of User objects



            connection.ExecuteNonQuery(
             query, parameters);


            Book book1 = GetBook(BookTitle, Author, Publisher, YearOfPublication);



            return View("ActionsForSelectedBook", book1);




        }


        public IActionResult DeleteBook(string BookTitle, string Author, string Publisher, int YearOfPublication)
        {


            var updateQuery = "DELETE FROM Books WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>{
                   { "@BookTitle", BookTitle },
                   { "@Author",  Author },
                   { "@Publisher",  Publisher },
                   { "@YearOfPublication",  YearOfPublication }};

            // Execute the update using ExecuteQuery
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
            connection.ExecuteNonQuery(updateQuery, parameters);

            TempData["MESSAGEdelete"] = "Book has been deleted successfuly";

            return View("ActionsForSelectedBook", new Book());

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


            int res= connection.ExecuteScalar<int>(query, parameters);

            return res > 0;
        }



        public IActionResult ShowBookDetails(string bookTitle, string author, string publisher, int yearOfPublication, bool isOnWaitingList)
        {
            Book book = GetBook(bookTitle, author, publisher, yearOfPublication);
            bool isBookOnWaitingList = IsBookOnWaitingList(book);


            if (isBookOnWaitingList)
            {
                TempData["Is book on borrowing waiting list"] = "yes";


            }
            else
            {
                TempData["Is book on borrowing waiting list"] = "no";
            }
            bool ans = IsUserOnWaitingList( book);
          

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

            return View("BookDetails", book);

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







        public IActionResult ShowBorrowingWaitingList(string bookTitle, string author, string publisher, int yearOfPublication, bool isOnWaitingList)
        {

          
            //ספירת כמה אנשים יש ברשימת המתנה עבור הספר הספציפי
            string query = "SELECT COUNT(1) FROM BorrowingBookWaitingList WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>
              {
                   { "@BookTitle", bookTitle },
                   { "@Author",  author },
                   { "@Publisher",  publisher },
                   { "@YearOfPublication",  yearOfPublication }
                };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            int amountInWaitingList = connection.ExecuteScalar<int>(query, parameters);

            BorrowingWaitingListViewModel model = new BorrowingWaitingListViewModel();

            model.AmountInWaitingList = amountInWaitingList;

            Book book = GetBook(bookTitle, author, publisher, yearOfPublication);

            model.book = book;

           bool ans = IsUserOnWaitingList(book);

            model.IsCurrentUserOnList = ans;

            model.ExpectedDaysUntilBookAvailable= AmountOfDaysTillBookAvailable(book);

            string email = HttpContext.Session.GetString("CurrentUser");

            ViewBag.Email = email;


            return View("BorrowingWaitingList", model);

        }


        public int AmountOfDaysTillBookAvailable(Book book)
        {

            var query = "SELECT MIN(BorrowDate) FROM BorrowedBooks WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            // Set up the parameters
            var parameters = new Dictionary<string, object>
            {
                  { "@BookTitle", book.BookTitle },
                   { "@Author",  book.Author },
                   { "@Publisher",  book.Publisher },
                   { "@YearOfPublication",  book.YearOfPublication }
            };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);


            object result = connection.ExecuteScalar<object>(query, parameters);

            if (result == null || result == DBNull.Value)
            { return 0; }

            DateTime earliestBorrowDate = Convert.ToDateTime(result);

            int daySinceFirstBorrowTillNow = (earliestBorrowDate - DateTime.Now).Days;

            return 30 - daySinceFirstBorrowTillNow;



        }





        public IActionResult AddUserToWaitingList(string Author, string BookTitle, string Publisher, int YearOfPublication, int AmountInWaitingList)
        {

            string insertQuery = @"
        INSERT INTO BorrowingBookWaitingList 
        (email, BookTitle, Author, Publisher, YearOfPublication, RequestDate, PlaceInQueue) 
        VALUES (@Email, @BookTitle, @Author, @Publisher, @YearOfPublication, @Date, @PlaceInQueue)";


            string email = HttpContext.Session.GetString("CurrentUser");
            var parameters = new Dictionary<string, object>
            {
                { "@Email", email },
                { "@BookTitle", BookTitle },
                { "@Author", Author },
                { "@Publisher", Publisher },
                { "@YearOfPublication", YearOfPublication },
                { "@Date", DateTime.Now },
                { "@PlaceInQueue", AmountInWaitingList + 1 }

            };

            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
            connection.ExecuteNonQuery(insertQuery, parameters);

            ViewBag.Message = "You Have been added to this book's Borrowing Waiting List";

            BorrowingWaitingListViewModel model = new BorrowingWaitingListViewModel();
            model.book = GetBook(Author, BookTitle, Publisher, YearOfPublication);

            return RedirectToAction("ShowBorrowingWaitingList", new { bookTitle = BookTitle, author = Author, publisher = Publisher, yearOfPublication = YearOfPublication, isOnWaitingList = true });
        }

        public void RemoveUserFromWaitingListAfter30days()
        {

            DateTime Today= DateTime.Now;

            var updateQuery = "DELETE FROM BorrowingBookWaitingList WHERE DATEDIFF(DAY, RequestDate, @Today) >= 30";


            var parameters = new Dictionary<string, object>{
                   {"@Today", Today},
               };

            // Execute the update using ExecuteQuery
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
            connection.ExecuteNonQuery(updateQuery, parameters);

          
        }


        
        public IActionResult RemoveUserFromWaitingList(string email, string Author, string BookTitle, string Publisher, int YearOfPublication)
        {
            var getPlaceInQueueQuery= "SELECT PlaceInQueue FROM BorrowingBookWaitingList WHERE email= @email AND BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication";      
            
            

            var parameters = new Dictionary<string, object>{
                   { "@email", email},
                   { "@BookTitle", BookTitle },
                   { "@Author",  Author },
                   { "@Publisher",Publisher },
                   { "@YearOfPublication", YearOfPublication }};

            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            int placeInQueue = connection.ExecuteScalar<int>(getPlaceInQueueQuery, parameters);




            string updateQuery = "DELETE FROM BorrowingBookWaitingList WHERE email= @email AND BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication";
           
            connection.ExecuteNonQuery(updateQuery, parameters);

            string updateQuery1 = "";
            var parameters1 = new Dictionary<string, object> { };

            if (placeInQueue == 1)
            {
                updateQuery1 = "UPDATE BorrowingBookWaitingList SET PlaceInQueue = PlaceInQueue - 1 WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication";
                parameters1 = new Dictionary<string, object>{
                   { "@BookTitle", BookTitle },
                   { "@Author",  Author },
                   { "@Publisher",Publisher },
                   { "@YearOfPublication", YearOfPublication} };
            }
            else
            {
                updateQuery1 = "UPDATE BorrowingBookWaitingList SET PlaceInQueue = PlaceInQueue - 1 WHERE PlaceInQueue>@deletedPlaceInQueue AND BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication";
                parameters1 = new Dictionary<string, object>{
                   {"@deletedPlaceInQueue", placeInQueue},
                    { "@BookTitle", BookTitle },
                   { "@Author",  Author },
                   { "@Publisher",Publisher },
                   { "@YearOfPublication", YearOfPublication} };

            }
                    


             connection.ExecuteNonQuery(updateQuery1, parameters1);


            TempData["removedFromWaitingList"] = "You have successfully been removed from this waiting list";

            return RedirectToAction("ShowBorrowingWaitingList", new { bookTitle= BookTitle, author=Author, publisher=Publisher, yearOfPublication= YearOfPublication, isOnWaitingList=false }   );



        }


        public void SendBorrowingReminders()
        {
            // SQL query to find borrowed books that are due in 5 days
            string query = @"
                SELECT UserName, BookTitle, ReturnDate, Email
                FROM BorrowedBooks
                WHERE DATEDIFF(DAY, GETDATE(), ReturnDate) = 5";

            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Execute the query and get the results
            var usersToRemind = connection.ExecuteQuery<BorrowedBook>(query, null, reader => new BorrowedBook
            {
                UserName = reader.GetString(reader.GetOrdinal("UserName")),
                BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                Author = reader.GetString(reader.GetOrdinal("Author")),
                Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                ReturnDate = reader.GetDateTime(reader.GetOrdinal("ReturnDate")),
                BorrowID = reader.GetInt32(reader.GetOrdinal("BorrowID")),
            });

            // Send email reminder for each borrowed book
            foreach (var item in usersToRemind)
            {
                // Send reminder only for borrowed books with remaining days == 5
                if (item.RemainingDays == 5)
                {

                    Gmail gmail = new Gmail();

                    gmail.To = item.UserName;
                    gmail.Subject = "Reminder: Your book borrowing period is ending soon!";
                    gmail.Body = $"Dear {item.UserName},\n\nThis is a reminder that the book '{item.BookTitle}' you borrowed is due to be returned in 5 days. Please make sure to return it on time.\n\nThank you.";

                    gmail.SendEmail();

                }
            }

         
        }
    }

}


