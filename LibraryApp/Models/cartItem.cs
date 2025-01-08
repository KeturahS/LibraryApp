namespace LibraryApp.Models
{
    public class cartItem
    {

        public string BookTitle { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public int YearOfPublication { get; set; }
        public string ActionType { get; set; }
        public DateTime AddedDate { get; set; }

       

        public decimal PriceForBuy { get; set; }
        public decimal PriceForBorrow { get; set; }
    }
}
