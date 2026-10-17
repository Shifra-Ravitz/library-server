using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'100', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Author = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PublicationYear = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'100', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'100', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxConcurrentLoans = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookCopies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'100', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    BookId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookCopies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookCopies_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookCategories",
                columns: table => new
                {
                    BookId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookCategories", x => new { x.BookId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_BookCategories_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MembershipPlanId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_MembershipPlans_MembershipPlanId",
                        column: x => x.MembershipPlanId,
                        principalTable: "MembershipPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    BookCopyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Loans_BookCopies_BookCopyId",
                        column: x => x.BookCopyId,
                        principalTable: "BookCopies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Loans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Books",
                columns: new[] { "Id", "Author", "IsDeleted", "PublicationYear", "Title" },
                values: new object[,]
                {
                    { 1, "רבי משה חיים לוצאטו (הרמח\"ל)", false, 1740, "מסילת ישרים" },
                    { 2, "רבנו בחיי אבן פקודה", false, 1489, "חובות הלבבות" },
                    { 3, "רבי יהודה הלוי", false, 1506, "הכוזרי" },
                    { 4, "רבי ישראל מאיר הכהן", false, 1873, "חפץ חיים" },
                    { 5, "רבי ישראל מאיר הכהן", false, 1876, "שמירת הלשון" },
                    { 6, "רבי ישראל מאיר הכהן", false, 1884, "משנה ברורה" },
                    { 7, "רבי שלמה גנצפריד", false, 1864, "קיצור שולחן ערוך" },
                    { 8, "רבי משה חיים לוצאטו (הרמח\"ל)", false, 1896, "דרך ה'" },
                    { 9, "רבי יחיאל היילפרין", false, 1769, "סדר הדורות" },
                    { 10, "רבי דוד גנז", false, 1592, "צמח דוד" }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "הלכה" },
                    { 2, "מוסר" },
                    { 3, "אמונה והשקפה" },
                    { 4, "תולדות ישראל" },
                    { 5, "ספרי יסוד" }
                });

            migrationBuilder.InsertData(
                table: "MembershipPlans",
                columns: new[] { "Id", "MaxConcurrentLoans", "Name", "Price" },
                values: new object[,]
                {
                    { 1, 1, "בסיסי", 0m },
                    { 2, 3, "רגיל", 30m },
                    { 3, 5, "מורחב", 50m }
                });

            migrationBuilder.InsertData(
                table: "BookCategories",
                columns: new[] { "BookId", "CategoryId" },
                values: new object[,]
                {
                    { 1, 2 },
                    { 1, 5 },
                    { 2, 2 },
                    { 2, 3 },
                    { 3, 3 },
                    { 3, 5 },
                    { 4, 1 },
                    { 4, 2 },
                    { 5, 2 },
                    { 6, 1 },
                    { 6, 5 },
                    { 7, 1 },
                    { 8, 3 },
                    { 9, 4 },
                    { 10, 4 }
                });

            migrationBuilder.InsertData(
                table: "BookCopies",
                columns: new[] { "Id", "BookId", "Status", "Version" },
                values: new object[,]
                {
                    { 1, 1, "Available", new Guid("00000000-0000-0000-0000-000000000001") },
                    { 2, 1, "Available", new Guid("00000000-0000-0000-0000-000000000002") },
                    { 3, 1, "Available", new Guid("00000000-0000-0000-0000-000000000003") },
                    { 4, 2, "Available", new Guid("00000000-0000-0000-0000-000000000004") },
                    { 5, 2, "Available", new Guid("00000000-0000-0000-0000-000000000005") },
                    { 6, 3, "Available", new Guid("00000000-0000-0000-0000-000000000006") },
                    { 7, 3, "Available", new Guid("00000000-0000-0000-0000-000000000007") },
                    { 8, 3, "Available", new Guid("00000000-0000-0000-0000-000000000008") },
                    { 9, 4, "Available", new Guid("00000000-0000-0000-0000-000000000009") },
                    { 10, 4, "Available", new Guid("00000000-0000-0000-0000-000000000010") },
                    { 11, 5, "Available", new Guid("00000000-0000-0000-0000-000000000011") },
                    { 12, 5, "Available", new Guid("00000000-0000-0000-0000-000000000012") },
                    { 13, 5, "Available", new Guid("00000000-0000-0000-0000-000000000013") },
                    { 14, 6, "Available", new Guid("00000000-0000-0000-0000-000000000014") },
                    { 15, 6, "Available", new Guid("00000000-0000-0000-0000-000000000015") },
                    { 16, 7, "Available", new Guid("00000000-0000-0000-0000-000000000016") },
                    { 17, 7, "Available", new Guid("00000000-0000-0000-0000-000000000017") },
                    { 18, 7, "Available", new Guid("00000000-0000-0000-0000-000000000018") },
                    { 19, 8, "Available", new Guid("00000000-0000-0000-0000-000000000019") },
                    { 20, 8, "Available", new Guid("00000000-0000-0000-0000-000000000020") },
                    { 21, 9, "Available", new Guid("00000000-0000-0000-0000-000000000021") },
                    { 22, 9, "Available", new Guid("00000000-0000-0000-0000-000000000022") },
                    { 23, 9, "Available", new Guid("00000000-0000-0000-0000-000000000023") },
                    { 24, 10, "Available", new Guid("00000000-0000-0000-0000-000000000024") },
                    { 25, 10, "Available", new Guid("00000000-0000-0000-0000-000000000025") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookCategories_CategoryId",
                table: "BookCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BookCopies_BookId",
                table: "BookCopies",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_BookCopyId",
                table: "Loans",
                column: "BookCopyId");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_UserId",
                table: "Loans",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_MembershipPlanId",
                table: "Users",
                column: "MembershipPlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookCategories");

            migrationBuilder.DropTable(
                name: "Loans");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "BookCopies");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Books");

            migrationBuilder.DropTable(
                name: "MembershipPlans");
        }
    }
}
