using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieNightPicker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingValueCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Rating_RatingValue_Range",
                table: "Ratings",
                sql: "\"RatingValue\" >= 1 AND \"RatingValue\" <= 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Rating_RatingValue_Range",
                table: "Ratings");
        }
    }
}
