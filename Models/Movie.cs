namespace PureAvocadoes.Models
{
    public class Movie
    {
        public int MovieId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        public ushort ReleaseYear { get; set; }

        public ushort DurationMinutes { get; set; }

        public string Director { get; set; } = string.Empty;

        public string Synopsis { get; set; } = string.Empty;

        public string? PosterUrl { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<RatingReview> Reviews { get; set; } = new List<RatingReview>();
    }
}
