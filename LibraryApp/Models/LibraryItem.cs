namespace LibraryApp.Models
{
    public class LibraryItem
    {
        public string BookTitle { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public int YearOfPublication { get; set; }
        public string ActionType { get; set; } // Purchased or Borrowed
        public DateTime Date { get; set; } // תאריך רכישה או השאלה

        public string ImageUrl { get; set; }
        public int? RemainingDays { get; set; } // ימים שנותרו להחזרה, עבור ספרים מושאלים בלבד

        


    }
}
