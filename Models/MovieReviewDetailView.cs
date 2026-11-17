namespace PureAvocadoes.Models
{
    public class MovieReviewDetailView
    {
        public int ReviewId { get; set; }

        public int UserId { get; set; }

        public string ReviewerName { get; set; } = string.Empty;

        public int MovieId { get; set; }

        public string MovieTitle { get; set; } = string.Empty;

        public byte Rating { get; set; }

        public string ReviewText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
