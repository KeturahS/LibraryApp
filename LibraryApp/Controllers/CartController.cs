//using LibraryApp.Models;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Data.SqlClient;

//public class CartController : Controller
//{
//    private readonly IConfiguration _configuration;
//    string connectionString;
//    public CartController(IConfiguration configuration)
//    {
//        _configuration = configuration;
//        connectionString = configuration.GetConnectionString("myConnect");
//    }

//    // פעולה להוספת ספר לעגלה
//    [HttpPost]
//    public IActionResult AddToCart(int bookId)
//    {
//        // קבלת עגלת הקניות הנוכחית מה-Session
//        List<int> cart = HttpContext.Session.GetObject<List<int>>("Cart") ?? new List<int>();

//        // הוספת הספר לעגלה
//        cart.Add(bookId);

//        // שמירת עגלת הקניות בחזרה ל-Session
//        HttpContext.Session.SetObject("Cart", cart);

//        TempData["SuccessMessage"] = "Book added to cart!";
//        return RedirectToAction("Index", "HomePage");
//    }

//    // פעולה להצגת עגלת הקניות
//    public IActionResult Index()
//    {
//        // שליפת הספרים מה-Session
//        List<int> cart = HttpContext.Session.GetObject<List<int>>("Cart") ?? new List<int>();

//        if (cart == null || !cart.Any())
//        {
//            TempData["ErrorMessage"] = "Your cart is empty.";
//            return View(new List<Book>());
//        }

//        // שליפת פרטי הספרים מהמסד
//        List<Book> books = new List<Book>();
//        using (SqlConnection connection = new SqlConnection(connectionString))
//        {
//            connection.Open();

//            // יצירת שאילתה בטוחה עם פרמטרים
//            string query = "SELECT * FROM Books WHERE Id IN (" + string.Join(",", cart.Select((id, index) => $"@Id{index}")) + ")";
//            using (SqlCommand command = new SqlCommand(query, connection))
//            {
//                // הוספת הפרמטרים לשאילתה
//                for (int i = 0; i < cart.Count; i++)
//                {
//                    command.Parameters.AddWithValue($"@Id{i}", cart[i]);
//                }

//                using (SqlDataReader reader = command.ExecuteReader())
//                {
//                    while (reader.Read())
//                    {
//                        books.Add(new Book
//                        {
//                            Id = reader.GetInt32(0),
//                            Title = reader.GetString(1),
//                            Author = reader.GetString(2),
//                            Publisher = reader.GetString(3),
//                            BorrowPrice = reader.GetDecimal(4),
//                            BuyPrice = reader.GetDecimal(5),
//                            AvailableCopies = reader.GetInt32(6),
//                            ImageUrl = reader.GetString(7)
//                        });
//                    }
//                }
//            }
//        }

//        return View(books);
//    }

//    // פעולה להסרת ספר מהעגלה
//    [HttpPost]
//    public IActionResult RemoveFromCart(int bookId)
//    {
//        List<int> cart = HttpContext.Session.GetObject<List<int>>("Cart") ?? new List<int>();
//        cart.Remove(bookId);
//        HttpContext.Session.SetObject("Cart", cart);

//        TempData["SuccessMessage"] = "Book removed from cart.";
//        return RedirectToAction("Index");
//    }
//}
