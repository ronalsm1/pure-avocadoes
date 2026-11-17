namespace PureAvocadoes.Models
{
    public class RatingReview
    {
        public int ReviewId { get; set; }

        public int UserId { get; set; }

        public int MovieId { get; set; }

        public byte Rating { get; set; }

        public string ReviewText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public User User { get; set; } = null!;

        public Movie Movie { get; set; } = null!;
    }
}
