namespace LibraryApp.Models
{
    public class Feedback
    {
        public string UserName { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime FeedbackDate { get; set; }
    }
}
