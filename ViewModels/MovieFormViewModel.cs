using PureAvocadoes.Dtos;
using System.ComponentModel.DataAnnotations;

namespace PureAvocadoes.ViewModels
{
    /// Admin Create/Edit movie form input ViewModel.
    /// Prevents over-posting protected fields such as CreatedAt or reviews.
    public class MovieFormViewModel
    {
        public int? MovieId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
        [Display(Name = "Movie Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Genre is required.")]
        [StringLength(80, MinimumLength = 1, ErrorMessage = "Genre must be between 1 and 80 characters.")]
        [Display(Name = "Genre")]
        public string Genre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Release year is required.")]
        [Range(1888, 2100, ErrorMessage = "Release year must be 1888 or later.")]
        [Display(Name = "Release Year")]
        public int? ReleaseYear { get; set; }

        [Required(ErrorMessage = "Duration is required.")]
        [Range(1, 1440, ErrorMessage = "Duration must be between 1 and 1,440 minutes.")]
        [Display(Name = "Duration (minutes)")]
        public int? DurationMinutes { get; set; }

        [Required(ErrorMessage = "Director is required.")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "Director must be between 1 and 150 characters.")]
        [Display(Name = "Director")]
        public string Director { get; set; } = string.Empty;

        [Required(ErrorMessage = "Synopsis is required.")]
        [StringLength(10000, MinimumLength = 1, ErrorMessage = "Synopsis is required.")]
        [Display(Name = "Synopsis")]
        public string Synopsis { get; set; } = string.Empty;

        [StringLength(2048, ErrorMessage = "Poster URL cannot exceed 2048 characters.")]
        [Display(Name = "Poster URL (optional)")]
        public string? PosterUrl { get; set; }

        public MovieUpsertDto ToUpsertDto() => new()
        {
            Title = Title,
            Genre = Genre,
            ReleaseYear = ReleaseYear,
            DurationMinutes = DurationMinutes,
            Director = Director,
            Synopsis = Synopsis,
            PosterUrl = PosterUrl
        };
    }
}
