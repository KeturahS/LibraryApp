namespace LibraryApp.Models
{
    public class Book
    {

        public int Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public decimal BorrowPrice { get; set; }
        public decimal BuyPrice { get; set; }
        public int AvailableCopies { get; set; }
        public string ImageUrl { get; set; }
    }
}
