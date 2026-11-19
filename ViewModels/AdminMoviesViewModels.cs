namespace PureAvocadoes.ViewModels
{
    /// ViewModel for the Admin Movies management list (/Admin/Movies).
    public class AdminMoviesIndexViewModel
    {
        public IReadOnlyList<MovieCardViewModel> Movies { get; set; } = Array.Empty<MovieCardViewModel>();

        public string? Search { get; set; }

        public bool HasDatabaseError { get; set; }

        public string? ErrorMessage { get; set; }
    }

    /// Confirmation ViewModel for deleting a movie (/Admin/Movies/Delete/{id}).
    public class AdminMovieDeleteViewModel
    {
        public MovieCardViewModel Movie { get; set; } = new();

        public long DependentReviewCount { get; set; }

        public bool CanDelete => DependentReviewCount == 0;
    }
}
