using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePayrollPeriodStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeePayrollStatus",
                table: "AccountingEmployeePayrolls",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql(
                """UPDATE "AccountingEmployeePayrolls" SET "EmployeePayrollStatus" = 'ACTIVE' WHERE "EmployeePayrollStatus" IS NULL OR trim("EmployeePayrollStatus") = '';""");

            migrationBuilder.AlterColumn<string>(
                name: "EmployeePayrollStatus",
                table: "AccountingEmployeePayrolls",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmployeePayrollStatusChangedAt",
                table: "AccountingEmployeePayrolls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeePayrollStatusChangedByUserId",
                table: "AccountingEmployeePayrolls",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeePayrollStatusReason",
                table: "AccountingEmployeePayrolls",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEmployeePayrolls_EmployeePayrollStatusChangedByUs~",
                table: "AccountingEmployeePayrolls",
                column: "EmployeePayrollStatusChangedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingEmployeePayrolls_Users_EmployeePayrollStatusChang~",
                table: "AccountingEmployeePayrolls",
                column: "EmployeePayrollStatusChangedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountingEmployeePayrolls_Users_EmployeePayrollStatusChang~",
                table: "AccountingEmployeePayrolls");

            migrationBuilder.DropIndex(
                name: "IX_AccountingEmployeePayrolls_EmployeePayrollStatusChangedByUs~",
                table: "AccountingEmployeePayrolls");

            migrationBuilder.DropColumn(
                name: "EmployeePayrollStatus",
                table: "AccountingEmployeePayrolls");

            migrationBuilder.DropColumn(
                name: "EmployeePayrollStatusChangedAt",
                table: "AccountingEmployeePayrolls");

            migrationBuilder.DropColumn(
                name: "EmployeePayrollStatusChangedByUserId",
                table: "AccountingEmployeePayrolls");

            migrationBuilder.DropColumn(
                name: "EmployeePayrollStatusReason",
                table: "AccountingEmployeePayrolls");
        }
    }
}
