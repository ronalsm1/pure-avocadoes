using System.ComponentModel.DataAnnotations;

namespace PureAvocadoes.Dtos
{
    public sealed record MovieDto(
        int MovieId,
        string Title,
        string Genre,
        ushort ReleaseYear,
        ushort DurationMinutes,
        string Director,
        string Synopsis,
        string? PosterUrl,
        long TotalReviews,
        decimal AverageRating,
        DateTime CreatedAt);

    public sealed class MovieUpsertDto
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Genre is required.")]
        [StringLength(80, MinimumLength = 1, ErrorMessage = "Genre must be between 1 and 80 characters.")]
        public string Genre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Release year is required.")]
        [Range(1888, 2100, ErrorMessage = "Release year must be 1888 or later.")]
        public int? ReleaseYear { get; set; }

        [Required(ErrorMessage = "Duration in minutes is required.")]
        [Range(1, 1440, ErrorMessage = "Duration must be between 1 and 1,440 minutes.")]
        public int? DurationMinutes { get; set; }

        [Required(ErrorMessage = "Director is required.")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "Director must be between 1 and 150 characters.")]
        public string Director { get; set; } = string.Empty;

        [Required(ErrorMessage = "Synopsis is required.")]
        [StringLength(10000, MinimumLength = 1, ErrorMessage = "Synopsis is required.")]
        public string Synopsis { get; set; } = string.Empty;

        [StringLength(2048, ErrorMessage = "Poster URL cannot exceed 2048 characters.")]
        public string? PosterUrl { get; set; }
    }

    public sealed record ApiErrorDto(
        string Message,
        string? Field = null,
        IReadOnlyDictionary<string, string[]>? Errors = null);
}
