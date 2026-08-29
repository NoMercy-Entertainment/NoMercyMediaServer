using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class LibraryLinkProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AddedAt",
                table: "LibraryTv",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                table: "LibraryTv",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                // Every row that already exists is recorded as brought in by a
                // file. That is the honest answer: nothing in the table said
                // which links the owner asked for, and inventing "manual" for
                // all of them would surface the artefacts this column exists to
                // tell apart. A show the owner did add reads as file-brought
                // until they add it again, which is the same state it was
                // already in.
                defaultValue: "file");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedAt",
                table: "LibraryTv");

            migrationBuilder.DropColumn(
                name: "AddedBy",
                table: "LibraryTv");
        }
    }
}
