namespace LibraryApp.Models
{
    public class BorrowedBook
    {
        public string UserName { get; set; }
        public string BookTitle { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public int YearOfPublication { get; set; }
        public DateTime BorrowDate { get; set; }
        public DateTime ReturnDate { get; set; }
        public int BorrowID { get; set; }
        public int RemainingDays => (ReturnDate - DateTime.Now).Days;
    }

}
