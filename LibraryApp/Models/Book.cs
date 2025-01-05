using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraryApp.Models
{
    public class Book
    {
       
        
        [Key]
        public string BookTitle { get; set; }

        [Key]
        public string Author { get; set; }

        [Key]
        public string Publisher { get; set; }

        [Key]
        public int YearOfPublication { get; set; }

        public  string Genre { get; set; }

        public decimal DISCOUNTEDPriceForBorrow { get; set; }
        public decimal DISCOUNTEDPriceForBuy { get; set; }

        public decimal PriceForBorrow { get; set; }
        public decimal PriceForBuy { get; set; }
        public  string AgeRestriction { get; set; }



		public bool IsOnSale { get; set; }
		public int AmountOfSaleDays { get; set; }

		public DateTime SaleStartDate { get; set; }
		public DateTime SaleEndDate { get; set; }

		public int AvailableAmountOfCopiesToBorrow { get; set; }

		public bool PDF { get; set; }
		public bool epub { get; set; }
		public bool f2b { get; set; }
		public bool mobi { get; set; }


		public int Popularity { get; set; }

        public string ImageUrl { get; set; }

        public bool BuyOnly {  get; set; }
    }
}
