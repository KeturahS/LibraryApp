using System.ComponentModel.DataAnnotations;

namespace LibraryApp.Models.ViewModel
{
	public class AdminViewModel
	{

      
        public  List<User> AdminUserRequests { get; set; }

		public  List<User> AllUsers { get; set; }


		public  User NewUser { get; set; }


        public List<Book> AllBooks { get; set; }


        public List<Book> SelectedBooks { get; set; }




    }
}
