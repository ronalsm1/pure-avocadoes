namespace PureAvocadoes.ViewModels
{
    public class MovieListViewModel
    {
        public IReadOnlyList<MovieCardViewModel> Movies { get; set; } = Array.Empty<MovieCardViewModel>();

        public string? Search { get; set; }

        public string? Genre { get; set; }

        public int? ReleaseYear { get; set; }

        public string? Sort { get; set; }

        public IReadOnlyList<string> AvailableGenres { get; set; } = Array.Empty<string>();

        public IReadOnlyList<int> AvailableReleaseYears { get; set; } = Array.Empty<int>();

        public int TotalCatalogCount { get; set; }

        public string? FilterWarning { get; set; }

        public bool HasDatabaseError { get; set; }

        public string? ErrorMessage { get; set; }

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search)
            || !string.IsNullOrWhiteSpace(Genre)
            || ReleaseYear.HasValue
            || !string.IsNullOrWhiteSpace(Sort);
    }
}
