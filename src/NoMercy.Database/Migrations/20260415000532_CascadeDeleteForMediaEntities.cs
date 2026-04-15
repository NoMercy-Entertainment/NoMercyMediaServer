using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteForMediaEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlternativeTitles_Movies_MovieId",
                table: "AlternativeTitles"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_AlternativeTitles_Tvs_TvId",
                table: "AlternativeTitles"
            );

            migrationBuilder.DropForeignKey(name: "FK_Casts_Episodes_EpisodeId", table: "Casts");

            migrationBuilder.DropForeignKey(name: "FK_Casts_Movies_MovieId", table: "Casts");

            migrationBuilder.DropForeignKey(name: "FK_Casts_Seasons_SeasonId", table: "Casts");

            migrationBuilder.DropForeignKey(name: "FK_Casts_Tvs_TvId", table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationMovie_Movies_MovieId",
                table: "CertificationMovie"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationTv_Tvs_TvId",
                table: "CertificationTv"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionMovie_Movies_MovieId",
                table: "CollectionMovie"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyMovie_Movies_MovieId",
                table: "CompanyMovie"
            );

            migrationBuilder.DropForeignKey(name: "FK_CompanyTv_Tvs_TvId", table: "CompanyTv");

            migrationBuilder.DropForeignKey(name: "FK_Creators_Tvs_TvId", table: "Creators");

            migrationBuilder.DropForeignKey(name: "FK_Crews_Episodes_EpisodeId", table: "Crews");

            migrationBuilder.DropForeignKey(name: "FK_Crews_Movies_MovieId", table: "Crews");

            migrationBuilder.DropForeignKey(name: "FK_Crews_Seasons_SeasonId", table: "Crews");

            migrationBuilder.DropForeignKey(name: "FK_Crews_Tvs_TvId", table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Episodes_Seasons_SeasonId",
                table: "Episodes"
            );

            migrationBuilder.DropForeignKey(name: "FK_Episodes_Tvs_TvId", table: "Episodes");

            migrationBuilder.DropForeignKey(name: "FK_GenreTv_Tvs_TvId", table: "GenreTv");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestStars_Episodes_EpisodeId",
                table: "GuestStars"
            );

            migrationBuilder.DropForeignKey(name: "FK_Images_Episodes_EpisodeId", table: "Images");

            migrationBuilder.DropForeignKey(name: "FK_Images_Movies_MovieId", table: "Images");

            migrationBuilder.DropForeignKey(name: "FK_Images_Seasons_SeasonId", table: "Images");

            migrationBuilder.DropForeignKey(name: "FK_Images_Tvs_TvId", table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordMovie_Movies_MovieId",
                table: "KeywordMovie"
            );

            migrationBuilder.DropForeignKey(name: "FK_KeywordTv_Tvs_TvId", table: "KeywordTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryMovie_Movies_MovieId",
                table: "LibraryMovie"
            );

            migrationBuilder.DropForeignKey(name: "FK_LibraryTv_Tvs_TvId", table: "LibraryTv");

            migrationBuilder.DropForeignKey(name: "FK_Medias_Episodes_EpisodeId", table: "Medias");

            migrationBuilder.DropForeignKey(name: "FK_Medias_Movies_MovieId", table: "Medias");

            migrationBuilder.DropForeignKey(name: "FK_Medias_Seasons_SeasonId", table: "Medias");

            migrationBuilder.DropForeignKey(name: "FK_Medias_Tvs_TvId", table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_VideoFiles_VideoFileId",
                table: "Medias"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_MovieUser_Movies_MovieId",
                table: "MovieUser"
            );

            migrationBuilder.DropForeignKey(name: "FK_NetworkTv_Tvs_TvId", table: "NetworkTv");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Movies_MovieId",
                table: "PlaybackPreferences"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Tvs_TvId",
                table: "PlaybackPreferences"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Movies_MovieFromId",
                table: "Recommendations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Movies_MovieToId",
                table: "Recommendations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Tvs_TvFromId",
                table: "Recommendations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Tvs_TvToId",
                table: "Recommendations"
            );

            migrationBuilder.DropForeignKey(name: "FK_Seasons_Movies_MovieId", table: "Seasons");

            migrationBuilder.DropForeignKey(name: "FK_Seasons_Tvs_TvId", table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Movies_MovieFromId",
                table: "Similar"
            );

            migrationBuilder.DropForeignKey(name: "FK_Similar_Movies_MovieToId", table: "Similar");

            migrationBuilder.DropForeignKey(name: "FK_Similar_Tvs_TvFromId", table: "Similar");

            migrationBuilder.DropForeignKey(name: "FK_Similar_Tvs_TvToId", table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Episodes_EpisodeId",
                table: "SpecialItems"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Movies_MovieId",
                table: "SpecialItems"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Episodes_EpisodeId",
                table: "Translations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Movies_MovieId",
                table: "Translations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Seasons_SeasonId",
                table: "Translations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Tvs_TvId",
                table: "Translations"
            );

            migrationBuilder.DropForeignKey(name: "FK_TvUser_Tvs_TvId", table: "TvUser");

            migrationBuilder.DropForeignKey(name: "FK_UserData_Tvs_TvId", table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Episodes_EpisodeId",
                table: "VideoFiles"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_Movies_MovieId",
                table: "WatchProviderMedia"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_Tvs_TvId",
                table: "WatchProviderMedia"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_AlternativeTitles_Movies_MovieId",
                table: "AlternativeTitles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_AlternativeTitles_Tvs_TvId",
                table: "AlternativeTitles",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Episodes_EpisodeId",
                table: "Casts",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Movies_MovieId",
                table: "Casts",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Seasons_SeasonId",
                table: "Casts",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Tvs_TvId",
                table: "Casts",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationMovie_Movies_MovieId",
                table: "CertificationMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationTv_Tvs_TvId",
                table: "CertificationTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionMovie_Movies_MovieId",
                table: "CollectionMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyMovie_Movies_MovieId",
                table: "CompanyMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyTv_Tvs_TvId",
                table: "CompanyTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Creators_Tvs_TvId",
                table: "Creators",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Episodes_EpisodeId",
                table: "Crews",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Movies_MovieId",
                table: "Crews",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Seasons_SeasonId",
                table: "Crews",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Tvs_TvId",
                table: "Crews",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Episodes_Seasons_SeasonId",
                table: "Episodes",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Episodes_Tvs_TvId",
                table: "Episodes",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_GenreTv_Tvs_TvId",
                table: "GenreTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_GuestStars_Episodes_EpisodeId",
                table: "GuestStars",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Episodes_EpisodeId",
                table: "Images",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Movies_MovieId",
                table: "Images",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Seasons_SeasonId",
                table: "Images",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Tvs_TvId",
                table: "Images",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordMovie_Movies_MovieId",
                table: "KeywordMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTv_Tvs_TvId",
                table: "KeywordTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryMovie_Movies_MovieId",
                table: "LibraryMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTv_Tvs_TvId",
                table: "LibraryTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Episodes_EpisodeId",
                table: "Medias",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Movies_MovieId",
                table: "Medias",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Seasons_SeasonId",
                table: "Medias",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Tvs_TvId",
                table: "Medias",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_VideoFiles_VideoFileId",
                table: "Medias",
                column: "VideoFileId",
                principalTable: "VideoFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_MovieUser_Movies_MovieId",
                table: "MovieUser",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkTv_Tvs_TvId",
                table: "NetworkTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Movies_MovieId",
                table: "PlaybackPreferences",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Tvs_TvId",
                table: "PlaybackPreferences",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Movies_MovieFromId",
                table: "Recommendations",
                column: "MovieFromId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Movies_MovieToId",
                table: "Recommendations",
                column: "MovieToId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Tvs_TvFromId",
                table: "Recommendations",
                column: "TvFromId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Tvs_TvToId",
                table: "Recommendations",
                column: "TvToId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Movies_MovieId",
                table: "Seasons",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Tvs_TvId",
                table: "Seasons",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Movies_MovieFromId",
                table: "Similar",
                column: "MovieFromId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Movies_MovieToId",
                table: "Similar",
                column: "MovieToId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Tvs_TvFromId",
                table: "Similar",
                column: "TvFromId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Tvs_TvToId",
                table: "Similar",
                column: "TvToId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Episodes_EpisodeId",
                table: "SpecialItems",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Movies_MovieId",
                table: "SpecialItems",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Episodes_EpisodeId",
                table: "Translations",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Movies_MovieId",
                table: "Translations",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Seasons_SeasonId",
                table: "Translations",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Tvs_TvId",
                table: "Translations",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TvUser_Tvs_TvId",
                table: "TvUser",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Tvs_TvId",
                table: "UserData",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Episodes_EpisodeId",
                table: "VideoFiles",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_Movies_MovieId",
                table: "WatchProviderMedia",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_Tvs_TvId",
                table: "WatchProviderMedia",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlternativeTitles_Movies_MovieId",
                table: "AlternativeTitles"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_AlternativeTitles_Tvs_TvId",
                table: "AlternativeTitles"
            );

            migrationBuilder.DropForeignKey(name: "FK_Casts_Episodes_EpisodeId", table: "Casts");

            migrationBuilder.DropForeignKey(name: "FK_Casts_Movies_MovieId", table: "Casts");

            migrationBuilder.DropForeignKey(name: "FK_Casts_Seasons_SeasonId", table: "Casts");

            migrationBuilder.DropForeignKey(name: "FK_Casts_Tvs_TvId", table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationMovie_Movies_MovieId",
                table: "CertificationMovie"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationTv_Tvs_TvId",
                table: "CertificationTv"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionMovie_Movies_MovieId",
                table: "CollectionMovie"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyMovie_Movies_MovieId",
                table: "CompanyMovie"
            );

            migrationBuilder.DropForeignKey(name: "FK_CompanyTv_Tvs_TvId", table: "CompanyTv");

            migrationBuilder.DropForeignKey(name: "FK_Creators_Tvs_TvId", table: "Creators");

            migrationBuilder.DropForeignKey(name: "FK_Crews_Episodes_EpisodeId", table: "Crews");

            migrationBuilder.DropForeignKey(name: "FK_Crews_Movies_MovieId", table: "Crews");

            migrationBuilder.DropForeignKey(name: "FK_Crews_Seasons_SeasonId", table: "Crews");

            migrationBuilder.DropForeignKey(name: "FK_Crews_Tvs_TvId", table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Episodes_Seasons_SeasonId",
                table: "Episodes"
            );

            migrationBuilder.DropForeignKey(name: "FK_Episodes_Tvs_TvId", table: "Episodes");

            migrationBuilder.DropForeignKey(name: "FK_GenreTv_Tvs_TvId", table: "GenreTv");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestStars_Episodes_EpisodeId",
                table: "GuestStars"
            );

            migrationBuilder.DropForeignKey(name: "FK_Images_Episodes_EpisodeId", table: "Images");

            migrationBuilder.DropForeignKey(name: "FK_Images_Movies_MovieId", table: "Images");

            migrationBuilder.DropForeignKey(name: "FK_Images_Seasons_SeasonId", table: "Images");

            migrationBuilder.DropForeignKey(name: "FK_Images_Tvs_TvId", table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordMovie_Movies_MovieId",
                table: "KeywordMovie"
            );

            migrationBuilder.DropForeignKey(name: "FK_KeywordTv_Tvs_TvId", table: "KeywordTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryMovie_Movies_MovieId",
                table: "LibraryMovie"
            );

            migrationBuilder.DropForeignKey(name: "FK_LibraryTv_Tvs_TvId", table: "LibraryTv");

            migrationBuilder.DropForeignKey(name: "FK_Medias_Episodes_EpisodeId", table: "Medias");

            migrationBuilder.DropForeignKey(name: "FK_Medias_Movies_MovieId", table: "Medias");

            migrationBuilder.DropForeignKey(name: "FK_Medias_Seasons_SeasonId", table: "Medias");

            migrationBuilder.DropForeignKey(name: "FK_Medias_Tvs_TvId", table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_VideoFiles_VideoFileId",
                table: "Medias"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_MovieUser_Movies_MovieId",
                table: "MovieUser"
            );

            migrationBuilder.DropForeignKey(name: "FK_NetworkTv_Tvs_TvId", table: "NetworkTv");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Movies_MovieId",
                table: "PlaybackPreferences"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Tvs_TvId",
                table: "PlaybackPreferences"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Movies_MovieFromId",
                table: "Recommendations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Movies_MovieToId",
                table: "Recommendations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Tvs_TvFromId",
                table: "Recommendations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Tvs_TvToId",
                table: "Recommendations"
            );

            migrationBuilder.DropForeignKey(name: "FK_Seasons_Movies_MovieId", table: "Seasons");

            migrationBuilder.DropForeignKey(name: "FK_Seasons_Tvs_TvId", table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Movies_MovieFromId",
                table: "Similar"
            );

            migrationBuilder.DropForeignKey(name: "FK_Similar_Movies_MovieToId", table: "Similar");

            migrationBuilder.DropForeignKey(name: "FK_Similar_Tvs_TvFromId", table: "Similar");

            migrationBuilder.DropForeignKey(name: "FK_Similar_Tvs_TvToId", table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Episodes_EpisodeId",
                table: "SpecialItems"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Movies_MovieId",
                table: "SpecialItems"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Episodes_EpisodeId",
                table: "Translations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Movies_MovieId",
                table: "Translations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Seasons_SeasonId",
                table: "Translations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Tvs_TvId",
                table: "Translations"
            );

            migrationBuilder.DropForeignKey(name: "FK_TvUser_Tvs_TvId", table: "TvUser");

            migrationBuilder.DropForeignKey(name: "FK_UserData_Tvs_TvId", table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Episodes_EpisodeId",
                table: "VideoFiles"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_Movies_MovieId",
                table: "WatchProviderMedia"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_Tvs_TvId",
                table: "WatchProviderMedia"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_AlternativeTitles_Movies_MovieId",
                table: "AlternativeTitles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_AlternativeTitles_Tvs_TvId",
                table: "AlternativeTitles",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Episodes_EpisodeId",
                table: "Casts",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Movies_MovieId",
                table: "Casts",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Seasons_SeasonId",
                table: "Casts",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Tvs_TvId",
                table: "Casts",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationMovie_Movies_MovieId",
                table: "CertificationMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationTv_Tvs_TvId",
                table: "CertificationTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionMovie_Movies_MovieId",
                table: "CollectionMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyMovie_Movies_MovieId",
                table: "CompanyMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyTv_Tvs_TvId",
                table: "CompanyTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Creators_Tvs_TvId",
                table: "Creators",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Episodes_EpisodeId",
                table: "Crews",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Movies_MovieId",
                table: "Crews",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Seasons_SeasonId",
                table: "Crews",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Tvs_TvId",
                table: "Crews",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Episodes_Seasons_SeasonId",
                table: "Episodes",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Episodes_Tvs_TvId",
                table: "Episodes",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_GenreTv_Tvs_TvId",
                table: "GenreTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_GuestStars_Episodes_EpisodeId",
                table: "GuestStars",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Episodes_EpisodeId",
                table: "Images",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Movies_MovieId",
                table: "Images",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Seasons_SeasonId",
                table: "Images",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Tvs_TvId",
                table: "Images",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordMovie_Movies_MovieId",
                table: "KeywordMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTv_Tvs_TvId",
                table: "KeywordTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryMovie_Movies_MovieId",
                table: "LibraryMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTv_Tvs_TvId",
                table: "LibraryTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Episodes_EpisodeId",
                table: "Medias",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Movies_MovieId",
                table: "Medias",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Seasons_SeasonId",
                table: "Medias",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Tvs_TvId",
                table: "Medias",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_VideoFiles_VideoFileId",
                table: "Medias",
                column: "VideoFileId",
                principalTable: "VideoFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_MovieUser_Movies_MovieId",
                table: "MovieUser",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkTv_Tvs_TvId",
                table: "NetworkTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Movies_MovieId",
                table: "PlaybackPreferences",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Tvs_TvId",
                table: "PlaybackPreferences",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Movies_MovieFromId",
                table: "Recommendations",
                column: "MovieFromId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Movies_MovieToId",
                table: "Recommendations",
                column: "MovieToId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Tvs_TvFromId",
                table: "Recommendations",
                column: "TvFromId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Tvs_TvToId",
                table: "Recommendations",
                column: "TvToId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Movies_MovieId",
                table: "Seasons",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Tvs_TvId",
                table: "Seasons",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Movies_MovieFromId",
                table: "Similar",
                column: "MovieFromId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Movies_MovieToId",
                table: "Similar",
                column: "MovieToId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Tvs_TvFromId",
                table: "Similar",
                column: "TvFromId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Tvs_TvToId",
                table: "Similar",
                column: "TvToId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Episodes_EpisodeId",
                table: "SpecialItems",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Movies_MovieId",
                table: "SpecialItems",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Episodes_EpisodeId",
                table: "Translations",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Movies_MovieId",
                table: "Translations",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Seasons_SeasonId",
                table: "Translations",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Tvs_TvId",
                table: "Translations",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_TvUser_Tvs_TvId",
                table: "TvUser",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Tvs_TvId",
                table: "UserData",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Episodes_EpisodeId",
                table: "VideoFiles",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_Movies_MovieId",
                table: "WatchProviderMedia",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_Tvs_TvId",
                table: "WatchProviderMedia",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }
    }
}
