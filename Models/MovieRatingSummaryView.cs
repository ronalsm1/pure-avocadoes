namespace PureAvocadoes.Models
{
    public class MovieRatingSummaryView
    {
        public int MovieId { get; set; }

        public string Title { get; set; } = string.Empty;

        public long TotalReviews { get; set; }

        public decimal AverageRating { get; set; }
    }
}
