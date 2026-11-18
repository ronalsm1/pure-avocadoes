using Microsoft.EntityFrameworkCore;
using PureAvocadoes.Models;

namespace PureAvocadoes.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<RatingReview> RatingsReviews => Set<RatingReview>();

    public DbSet<MovieRatingSummaryView> MovieRatingSummaries => Set<MovieRatingSummaryView>();

    public DbSet<MovieReviewDetailView> MovieReviewDetails => Set<MovieReviewDetailView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUser(modelBuilder);
        ConfigureMovie(modelBuilder);
        ConfigureRatingReview(modelBuilder);
        ConfigureViews(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", t =>
                t.HasCheckConstraint("chk_users_role", "role IN ('User', 'Admin')"));

            entity.HasKey(u => u.UserId);

            entity.Property(u => u.UserId)
                .HasColumnName("user_id")
                .HasColumnType("int")
                .ValueGeneratedOnAdd();

            entity.Property(u => u.FullName)
                .HasColumnName("full_name")
                .HasColumnType("varchar(120)")
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(u => u.Email)
                .HasColumnName("email")
                .HasColumnType("varchar(254)")
                .HasMaxLength(254)
                .IsRequired();

            entity.Property(u => u.PasswordHash)
                .HasColumnName("password_hash")
                .HasColumnType("varchar(255)")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(u => u.Role)
                .HasColumnName("role")
                .HasColumnType("varchar(20)")
                .HasMaxLength(20)
                .HasDefaultValue("User")
                .IsRequired();

            entity.Property(u => u.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(6)")
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .ValueGeneratedOnAdd()
                .IsRequired();

            entity.HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("uq_users_email");
        });
    }

    private static void ConfigureMovie(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.ToTable("movies", t =>
            {
                t.HasCheckConstraint("chk_movies_release_year", "release_year >= 1888");
                t.HasCheckConstraint("chk_movies_duration", "duration_minutes > 0");
            });

            entity.HasKey(m => m.MovieId);

            entity.Property(m => m.MovieId)
                .HasColumnName("movie_id")
                .HasColumnType("int")
                .ValueGeneratedOnAdd();

            entity.Property(m => m.Title)
                .HasColumnName("title")
                .HasColumnType("varchar(200)")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(m => m.Genre)
                .HasColumnName("genre")
                .HasColumnType("varchar(80)")
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(m => m.ReleaseYear)
                .HasColumnName("release_year")
                .HasColumnType("smallint unsigned")
                .IsRequired();

            entity.Property(m => m.DurationMinutes)
                .HasColumnName("duration_minutes")
                .HasColumnType("smallint unsigned")
                .IsRequired();

            entity.Property(m => m.Director)
                .HasColumnName("director")
                .HasColumnType("varchar(150)")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(m => m.Synopsis)
                .HasColumnName("synopsis")
                .HasColumnType("text")
                .IsRequired();

            entity.Property(m => m.PosterUrl)
                .HasColumnName("poster_url")
                .HasColumnType("varchar(2048)")
                .HasMaxLength(2048)
                .IsRequired(false);

            entity.Property(m => m.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(6)")
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .ValueGeneratedOnAdd()
                .IsRequired();

            entity.HasIndex(m => m.Title)
                .HasDatabaseName("ix_movies_title");

            entity.HasIndex(m => new { m.Genre, m.ReleaseYear })
                .HasDatabaseName("ix_movies_genre_release_year");
        });
    }

    private static void ConfigureRatingReview(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RatingReview>(entity =>
        {
            entity.ToTable("ratings_reviews", t =>
                t.HasCheckConstraint("chk_reviews_rating", "rating BETWEEN 1 AND 5"));

            entity.HasKey(r => r.ReviewId);

            entity.Property(r => r.ReviewId)
                .HasColumnName("review_id")
                .HasColumnType("int")
                .ValueGeneratedOnAdd();

            entity.Property(r => r.UserId)
                .HasColumnName("user_id")
                .HasColumnType("int")
                .IsRequired();

            entity.Property(r => r.MovieId)
                .HasColumnName("movie_id")
                .HasColumnType("int")
                .IsRequired();

            entity.Property(r => r.Rating)
                .HasColumnName("rating")
                .HasColumnType("tinyint unsigned")
                .IsRequired();

            entity.Property(r => r.ReviewText)
                .HasColumnName("review_text")
                .HasColumnType("varchar(2000)")
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(r => r.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(6)")
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .ValueGeneratedOnAdd()
                .IsRequired();

            entity.Property(r => r.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("datetime(6)")
                .IsRequired(false);

            entity.HasIndex(r => new { r.UserId, r.MovieId })
                .IsUnique()
                .HasDatabaseName("uq_reviews_user_movie");

            entity.HasIndex(r => r.MovieId)
                .HasDatabaseName("ix_reviews_movie_id");

            entity.HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .HasConstraintName("fk_reviews_user")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Movie)
                .WithMany(m => m.Reviews)
                .HasForeignKey(r => r.MovieId)
                .HasConstraintName("fk_reviews_movie")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureViews(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MovieRatingSummaryView>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_movie_rating_summary");

            entity.Property(v => v.MovieId).HasColumnName("movie_id");
            entity.Property(v => v.Title).HasColumnName("title");
            entity.Property(v => v.TotalReviews).HasColumnName("total_reviews");
            entity.Property(v => v.AverageRating).HasColumnName("average_rating");
        });

        modelBuilder.Entity<MovieReviewDetailView>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_movie_review_details");

            entity.Property(v => v.ReviewId).HasColumnName("review_id");
            entity.Property(v => v.UserId).HasColumnName("user_id");
            entity.Property(v => v.ReviewerName).HasColumnName("reviewer_name");
            entity.Property(v => v.MovieId).HasColumnName("movie_id");
            entity.Property(v => v.MovieTitle).HasColumnName("movie_title");
            entity.Property(v => v.Rating).HasColumnName("rating");
            entity.Property(v => v.ReviewText).HasColumnName("review_text");
            entity.Property(v => v.CreatedAt).HasColumnName("created_at");
            entity.Property(v => v.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
