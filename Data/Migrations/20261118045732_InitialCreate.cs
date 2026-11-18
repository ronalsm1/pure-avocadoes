using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace PureAvocadoes.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "movies",
                columns: table => new
                {
                    movie_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    genre = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    release_year = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    duration_minutes = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    director = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    synopsis = table.Column<string>(type: "text", nullable: false),
                    poster_url = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movies", x => x.movie_id);
                    table.CheckConstraint("chk_movies_duration", "duration_minutes > 0");
                    table.CheckConstraint("chk_movies_release_year", "release_year >= 1888");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    full_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "User"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                    table.CheckConstraint("chk_users_role", "role IN ('User', 'Admin')");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ratings_reviews",
                columns: table => new
                {
                    review_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    movie_id = table.Column<int>(type: "int", nullable: false),
                    rating = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    review_text = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ratings_reviews", x => x.review_id);
                    table.CheckConstraint("chk_reviews_rating", "rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_reviews_movie",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "movie_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_movies_genre_release_year",
                table: "movies",
                columns: new[] { "genre", "release_year" });

            migrationBuilder.CreateIndex(
                name: "ix_movies_title",
                table: "movies",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_movie_id",
                table: "ratings_reviews",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "uq_reviews_user_movie",
                table: "ratings_reviews",
                columns: new[] { "user_id", "movie_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ratings_reviews");

            migrationBuilder.DropTable(
                name: "movies");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
