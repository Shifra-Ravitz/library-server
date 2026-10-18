using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLoanQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveLoansCount",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // העמודה החדשה נוצרת עם 0 לכל המשתמשות, אבל בבסיס הנתונים כבר יכולות להיות השאלות פעילות.
            // בלי העדכון הזה, משתמשת עם השאלה פעילה תוכל לשאול מעבר למכסה,
            // וכשהיא תחזיר את ההשאלה המונה כבר יהיה 0 ולא ירד (ייכתב רק LogWarning).
            // לכן המונה מחושב פעם אחת מההשאלות שעוד לא הוחזרו.
            migrationBuilder.Sql(
                """
                UPDATE "Users" AS u
                SET "ActiveLoansCount" = (
                    SELECT COUNT(*)
                    FROM "Loans" AS l
                    WHERE l."UserId" = u."Id" AND l."ReturnedAtUtc" IS NULL
                );
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_ActiveLoansCount_NonNegative",
                table: "Users",
                sql: "\"ActiveLoansCount\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_ActiveLoansCount_NonNegative",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ActiveLoansCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Users");
        }
    }
}
