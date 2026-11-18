using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PureAvocadoes.Models;

namespace PureAvocadoes.Data;

public static class DatabaseSeeder
{
    public const string DefaultSeedAdminEmail = "admin@pureavocadoes.local";
    public const string DefaultSeedUserEmail = "reviewer@pureavocadoes.local";

    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        var db = services.GetRequiredService<ApplicationDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("ProjectCoba.Data.DatabaseSeeder");

        var adminEmail = NormalizeEmail(configuration["SeedData:AdminEmail"], DefaultSeedAdminEmail);
        var adminPassword = configuration["SeedData:AdminPassword"];

        var demoUserEmail = NormalizeEmail(configuration["SeedData:DemoUserEmail"], DefaultSeedUserEmail);
        var demoUserPassword = configuration["SeedData:DemoUserPassword"];

        var adminUser = await EnsureSeedUserAsync(
            db,
            passwordHasher,
            logger,
            fullName: "Ini Admin",
            email: adminEmail,
            password: adminPassword,
            passwordConfigKey: "SeedData:AdminPassword",
            role: "Admin",
            cancellationToken);

        var demoUser = await EnsureSeedUserAsync(
            db,
            passwordHasher,
            logger,
            fullName: "Ini User",
            email: demoUserEmail,
            password: demoUserPassword,
            passwordConfigKey: "SeedData:DemoUserPassword",
            role: "User",
            cancellationToken);

        var seededMovies = await EnsureSeedMoviesAsync(db, cancellationToken);

        await EnsureSeedReviewsAsync(db, seededMovies, demoUser, adminUser, cancellationToken);
    }

    private static string NormalizeEmail(string? configured, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? fallback : configured;
        return value.Trim().ToLowerInvariant();
    }

    private static async Task<User?> EnsureSeedUserAsync(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        ILogger logger,
        string fullName,
        string email,
        string? password,
        string passwordConfigKey,
        string role,
        CancellationToken cancellationToken)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Seed credential {ConfigKey} is not configured; skipping demo {Role} account seeding.",
                passwordConfigKey,
                role);
            return null;
        }

        var user = new User
        {
            FullName = fullName,
            Email = email,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded demo {Role} account ({Email}).", role, email);
        return user;
    }

    private static async Task<Dictionary<string, int>> EnsureSeedMoviesAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var sampleMovies = new[]
        {
            new Movie
            {
                Title = "Wild Horse Nine",
                Genre = "Comedy/Drama/Crime/Mystery & Thriller",
                ReleaseYear = 2026,
                DurationMinutes = 116,
                Director = "Martin McDonagh",
                Synopsis = "Shortly before the 1973 Chilean coup, CIA agents Chris (John Malkovich) and Lee (Sam Rockwell) are dispatched from Santiago to Easter island by their bureau chief, MJ (Steve Buscemi). Amongst the Island's iconic statues, and as the longtime partners wrestle with their dark pasts and present conspiracies, Chris's newfound bond with a pair of rebellious students (Mariana di Girolamo and Ailín Salas) threatens to send everyone’s trip to this remote paradise sideways.",
                PosterUrl = null,
                CreatedAt = DateTime.UtcNow
            },
        };

        var existingMovies = await db.Movies
            .Select(m => new { m.MovieId, m.Title, m.ReleaseYear })
            .ToListAsync(cancellationToken);

        var existingKeys = existingMovies
            .Select(m => (Title: m.Title.ToLowerInvariant(), m.ReleaseYear))
            .ToHashSet();

        var movieIdsByTitle = existingMovies
            .GroupBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().MovieId, StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var candidate in sampleMovies)
        {
            var key = (candidate.Title.ToLowerInvariant(), candidate.ReleaseYear);
            if (!existingKeys.Contains(key))
            {
                db.Movies.Add(candidate);
                existingKeys.Add(key);
                added = true;
            }
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var movie in db.ChangeTracker.Entries<Movie>().Select(e => e.Entity))
            {
                movieIdsByTitle.TryAdd(movie.Title, movie.MovieId);
            }
        }

        return movieIdsByTitle;
    }

    private static async Task EnsureSeedReviewsAsync(
        ApplicationDbContext db,
        Dictionary<string, int> movieIdsByTitle,
        User? demoUser,
        User? adminUser,
        CancellationToken cancellationToken)
    {
        if (demoUser is null && adminUser is null)
        {
            return;
        }

        var candidateReviews = new List<(int UserId, string MovieTitle, byte Rating, string ReviewText)>();

        if (demoUser is not null)
        {
            candidateReviews.Add((demoUser.UserId, "Wild Horse Nine", 5, "Very Good."));

        }

        if (adminUser is not null)
        {
            candidateReviews.Add((adminUser.UserId, "Wild Horse Nine", 5, "Very Good, Very Well."));
            
        }

        // Leave "Cedar Ridge Signal (Demo)" unreviewed initially to exercise 0-review LEFT JOIN behavior.
        var existingPairs = await db.RatingsReviews
            .Select(r => new { r.UserId, r.MovieId })
            .ToListAsync(cancellationToken);

        var pairSet = existingPairs
            .Select(p => (p.UserId, p.MovieId))
            .ToHashSet();

        var added = false;
        foreach (var item in candidateReviews)
        {
            if (!movieIdsByTitle.TryGetValue(item.MovieTitle, out var movieId))
            {
                continue;
            }

            if (pairSet.Contains((item.UserId, movieId)))
            {
                continue;
            }

            db.RatingsReviews.Add(new RatingReview
            {
                UserId = item.UserId,
                MovieId = movieId,
                Rating = item.Rating,
                ReviewText = item.ReviewText,
                CreatedAt = DateTime.UtcNow
            });
            pairSet.Add((item.UserId, movieId));
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
