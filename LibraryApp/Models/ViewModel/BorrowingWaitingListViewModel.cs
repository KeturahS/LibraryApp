using static System.Reflection.Metadata.BlobBuilder;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace LibraryApp.Models.ViewModel
{
    public class BorrowingWaitingListViewModel
    {
            public string email { get; set; } // כתובת מייל של המשתמש
            public string BookTitle { get; set; } // שם הספר
            public string Author { get; set; } // מחבר הספר
            public string Publisher { get; set; } // הוצאת הספר
            public int YearOfPublication { get; set; } // שנת פרסום
            public DateTime RequestDate { get; set; } = DateTime.Now; // תאריך הבקשה

            public int PlaceInQueue { get; set; } // מקום בתור (אופציונלי)

            public string  CurrentUserEmail { get; set; }

            public Book book { get; set; }

            public int AmountInWaitingList { get; set; }


            public int ExpectedDaysUntilBookAvailable { get; set; }

            public bool IsCurrentUserOnList { get; set; }

        
    }
}
