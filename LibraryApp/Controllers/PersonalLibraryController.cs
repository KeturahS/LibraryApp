using LibraryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Text;

using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using static LibraryApp.Models.ConnectionToDBmodel;
using System.Security.Policy;

public class PersonalLibraryController : Controller
{
    private readonly IConfiguration _configuration;
    private string connectionString;

    public PersonalLibraryController(IConfiguration configuration)
    {
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("myConnect");
    }

    public IActionResult showPersonalLibrary()
    {
        string userName = HttpContext.Session.GetString("CurrentUser");
        if (string.IsNullOrEmpty(userName))
        {
            TempData["ErrorMessage"] = "You must be logged in to view your personal library.";
            return RedirectToAction("SignIn", "HomePage");
        }

        List<LibraryItem> libraryItems = new List<LibraryItem>();

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // שליפת הספרים שנרכשו
            string purchasedQuery = @"
                SELECT BookTitle, Author, Publisher, YearOfPublication, 'Purchased' AS ActionType, PurchaseDate AS Date
                FROM PurchasedBooks
                WHERE UserName = @UserName";

            using (SqlCommand command = new SqlCommand(purchasedQuery, connection))
            {
                command.Parameters.AddWithValue("@UserName", userName);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        libraryItems.Add(new LibraryItem
                        {
                            BookTitle = reader.GetString(0),
                            Author = reader.GetString(1),
                            Publisher = reader.GetString(2),
                            YearOfPublication = reader.GetInt32(3),
                            ActionType = reader.GetString(4),
                            Date = reader.GetDateTime(5),
                            RemainingDays = null // ספרים שנרכשו לא דורשים החזרה
                        });
                    }
                }
            }

            // שליפת הספרים שהושאלו
            string borrowedQuery = @"
                SELECT BookTitle, Author, Publisher, YearOfPublication, 'Borrowed' AS ActionType, BorrowDate AS Date, 
                       DATEDIFF(DAY, GETDATE(), ReturnDate) AS RemainingDays
                FROM BorrowedBooks
                WHERE UserName = @UserName";

            using (SqlCommand command = new SqlCommand(borrowedQuery, connection))
            {
                command.Parameters.AddWithValue("@UserName", userName);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int remainingDays = reader.GetInt32(6);
                        if (remainingDays > 0)
                        {
                            libraryItems.Add(new LibraryItem
                            {
                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                ActionType = reader.GetString(4),
                                Date = reader.GetDateTime(5),
                                RemainingDays = remainingDays
                            });
                        }
                    }
                }
            }
        }

        return View("showPersonalLibrary",libraryItems);
    }

    [HttpPost]
    public IActionResult RemoveBook(string bookTitle, string actionType)
    {
        string userName = HttpContext.Session.GetString("CurrentUser");
        if (string.IsNullOrEmpty(userName))
        {
            TempData["ErrorMessage"] = "You must be logged in to remove a book.";
            return RedirectToAction("SignIn", "HomePage");
        }

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            string deleteQuery;

            if (actionType == "Purchased")
            {
                deleteQuery = "DELETE FROM PurchasedBooks WHERE UserName = @UserName AND BookTitle = @BookTitle";
            }
            else if (actionType == "Borrowed")
            {
                deleteQuery = "DELETE FROM BorrowedBooks WHERE UserName = @UserName AND BookTitle = @BookTitle";
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid action type.";
                return RedirectToAction("showPersonalLibrary");
            }

            using (SqlCommand command = new SqlCommand(deleteQuery, connection))
            {
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@BookTitle", bookTitle);
                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Book removed successfully.";

                    if (actionType == "Borrowed")
                    {
                       // UpdateAboutNewAvailableBook(bookTitle, Author, Publisher, YearOfPublication);   /// כאן בעצם שולחים לשלושה אנשים ראשונים שברשימת ההמתנה לספר הזה שהספר זמין אבל אפשר לעשות את זה רק אם ארבעת השדות של המפתח של הספר מתקבלים כפרמטרים בפונקציה הזאת
                                                                                                            ////ברגע שאתה מסדר את הטבלה אז אפשר פשוט להוציא את זה מההערות

					}
                }

                

                
                else
                {
                    TempData["ErrorMessage"] = "Failed to remove the book.";
                }
            }
        }

        return RedirectToAction("showPersonalLibrary");
    }



	public void UpdateAboutNewAvailableBook(string BookTitle, string Author, string Publisher, int YearOfPublication)
	{

		string query = "SELECT email FROM BorrowingBookWaitingList WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication AND PlaceInQueue<4";

		var parameters = new Dictionary<string, object>
	   {
			{ "@BookTitle", BookTitle },
			{ "@Author", Author },
			{ "@Publisher", Publisher },
			{ "@YearOfPublication", YearOfPublication }
		 };

		// Create a connection to the database
		ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

		var emails = connection.ExecuteQuery<string>(
			   query,
			   parameters,
			   reader => reader.GetString(0) // הנחה שהעמודה הראשונה היא האימייל
		   );


		foreach (var email in emails)
		{

			Gmail gmail = new Gmail();

			gmail.To = email;
			gmail.Subject = "The book you have requested to borrow is now available!!!";
			gmail.Body = "Dear user " + email + " we are glad to inform you that the book " + BookTitle + " year " + YearOfPublication + ", by the author " + Author + " and publisher " + Publisher + " is available now for borrowing. Hurry and make the payment as soon as possible seeing as other users on the waiting list are being notified as well.";

			gmail.SendEmail();


		}


	}





	public IActionResult AddFeedback(string bookTitle, string author, string publisher, int yearOfPublication)
    {
        string userName = HttpContext.Session.GetString("CurrentUser");

        // בדיקה אם המשתמש רכש או השאיל את הספר
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            string query = @"
            SELECT COUNT(*) FROM (
                SELECT BookTitle FROM PurchasedBooks WHERE UserName = @UserName AND BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication
                UNION
                SELECT BookTitle FROM BorrowedBooks WHERE UserName = @UserName AND BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication
            ) AS UserBooks";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@BookTitle", bookTitle);
                command.Parameters.AddWithValue("@Author", author);
                command.Parameters.AddWithValue("@Publisher", publisher);
                command.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                int count = (int)command.ExecuteScalar();
                if (count == 0)
                {
                    TempData["ErrorMessage"] = "You can only give feedback for books you have purchased or borrowed.";
                    return RedirectToAction("showPersonalLibrary", "PersonalLibrary");
                }
            }
        }

        ViewBag.BookTitle = bookTitle;
        ViewBag.Author = author;
        ViewBag.Publisher = publisher;
        ViewBag.YearOfPublication = yearOfPublication;

        return View("AddFeedback");
    }


    [HttpPost]
    public IActionResult SubmitFeedback(string bookTitle, string author, string publisher, int yearOfPublication, int rating, string feedback)
    {
        string userName = HttpContext.Session.GetString("CurrentUser");

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            string query = @"
            INSERT INTO BookFeedback (UserName, BookTitle, Author, Publisher, YearOfPublication, Rating, Feedback, FeedbackDate)
            VALUES (@UserName, @BookTitle, @Author, @Publisher, @YearOfPublication, @Rating, @Feedback, GETDATE())";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@BookTitle", bookTitle);
                command.Parameters.AddWithValue("@Author", author);
                command.Parameters.AddWithValue("@Publisher", publisher);
                command.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);
                command.Parameters.AddWithValue("@Rating", rating);
                command.Parameters.AddWithValue("@Feedback", feedback);

                int rowsAffected = command.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Feedback submitted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to submit feedback. Please try again.";
                }
            }
        }
        
        return RedirectToAction("showPersonalLibrary", "PersonalLibrary");
    }





    private string GetContentType(string format)
    {
        return format.ToLower() switch
        {
            "pdf" => "application/pdf",
            "epub" => "application/epub+zip",
            "f2b" => "application/octet-stream",
            "mobi" => "application/x-mobipocket-ebook",
            _ => "application/octet-stream"
        };
    }

    private byte[] GenerateDummyFileContent(string format, string bookTitle)
    {
        string dummyContent = $"This is a placeholder file for the book '{bookTitle}' in {format.ToUpper()} format.";
        return Encoding.UTF8.GetBytes(dummyContent);
    }

    public IActionResult DownloadBook(string bookTitle, string format)
    {
        if (string.IsNullOrEmpty(bookTitle) || string.IsNullOrEmpty(format))
        {
            TempData["ErrorMessage"] = "Invalid request.";
            return RedirectToAction("ShowPersonalLibrary");
        }

        if (format.ToLower() == "pdf")
        {
            using (var memoryStream = new MemoryStream())
            {
                // יצירת מסמך PDF
                PdfWriter writer = new PdfWriter(memoryStream);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf);

                // הוספת טקסט לקובץ ה-PDF
                document.Add(new Paragraph($"This is a placeholder PDF file for the book '{bookTitle}'."));

                document.Close();

                // החזרת הקובץ כקובץ להורדה
                return File(memoryStream.ToArray(), "application/pdf", $"{bookTitle}.pdf");
            }
        }

        string fileName = $"{bookTitle}.{format}";
        string contentType = GetContentType(format);
        byte[] fileContent = GenerateDummyFileContent(format, bookTitle);

        return File(fileContent, contentType, fileName);
    }


}
