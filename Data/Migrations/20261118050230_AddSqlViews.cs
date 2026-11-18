
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PureAvocadoes.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSqlViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // View 1: Movie Rating Summary
            // Menampilkan rata-rata rating dan jumlah review setiap film.
            // Film tanpa review tetap ditampilkan dengan nilai 0.

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE VIEW vw_movie_rating_summary AS
                SELECT
                    m.movie_id AS movie_id,
                    m.title AS title,
                    CAST(
                        COALESCE(ROUND(AVG(r.rating), 2), 0.00)
                        AS DECIMAL(18, 2)
                    ) AS average_rating,
                    CAST(COUNT(r.review_id) AS SIGNED) AS total_reviews
                FROM movies AS m
                LEFT JOIN ratings_reviews AS r
                    ON r.movie_id = m.movie_id
                GROUP BY
                    m.movie_id,
                    m.title;
                """);

            // View 2: Movie Review Details
            // Menggabungkan data review, film, dan nama reviewer.

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE VIEW vw_movie_review_details AS
                SELECT
                    r.review_id AS review_id,
                    r.user_id AS user_id,
                    r.movie_id AS movie_id,
                    m.title AS movie_title,
                    u.full_name AS reviewer_name,
                    r.rating AS rating,
                    r.review_text AS review_text,
                    r.created_at AS created_at,
                    r.updated_at AS updated_at
                FROM ratings_reviews AS r
                INNER JOIN movies AS m
                    ON r.movie_id = m.movie_id
                INNER JOIN users AS u
                    ON r.user_id = u.user_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hapus View 2 terlebih dahulu.
            migrationBuilder.Sql(
                "DROP VIEW IF EXISTS vw_movie_review_details;");

            // Kemudian hapus View 1.
            migrationBuilder.Sql(
                "DROP VIEW IF EXISTS vw_movie_rating_summary;");
        }
    }
}
