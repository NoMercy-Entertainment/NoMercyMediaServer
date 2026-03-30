using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercy.Database.Migrations
{
    /// <inheritdoc />
    public partial class RestrictCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Devices_DeviceId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Users_UserId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtist_Albums_AlbumId",
                table: "AlbumArtist");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtist_Artists_ArtistId",
                table: "AlbumArtist");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumLibrary_Albums_AlbumId",
                table: "AlbumLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumLibrary_Libraries_LibraryId",
                table: "AlbumLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumMusicGenre_Albums_AlbumId",
                table: "AlbumMusicGenre");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumMusicGenre_MusicGenres_MusicGenreId",
                table: "AlbumMusicGenre");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumReleaseGroup_Albums_AlbumId",
                table: "AlbumReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "AlbumReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Folders_FolderId",
                table: "Albums");

            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Libraries_LibraryId",
                table: "Albums");

            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Metadata_MetadataId",
                table: "Albums");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumTrack_Albums_AlbumId",
                table: "AlbumTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumTrack_Tracks_TrackId",
                table: "AlbumTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumUser_Albums_AlbumId",
                table: "AlbumUser");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumUser_Users_UserId",
                table: "AlbumUser");

            migrationBuilder.DropForeignKey(
                name: "FK_AlternativeTitles_Movies_MovieId",
                table: "AlternativeTitles");

            migrationBuilder.DropForeignKey(
                name: "FK_AlternativeTitles_Tvs_TvId",
                table: "AlternativeTitles");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistLibrary_Artists_ArtistId",
                table: "ArtistLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistLibrary_Libraries_LibraryId",
                table: "ArtistLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistMusicGenre_Artists_ArtistId",
                table: "ArtistMusicGenre");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistMusicGenre_MusicGenres_MusicGenreId",
                table: "ArtistMusicGenre");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistReleaseGroup_Artists_ArtistId",
                table: "ArtistReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "ArtistReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_Artists_Folders_FolderId",
                table: "Artists");

            migrationBuilder.DropForeignKey(
                name: "FK_Artists_Libraries_LibraryId",
                table: "Artists");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistTrack_Artists_ArtistId",
                table: "ArtistTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistTrack_Tracks_TrackId",
                table: "ArtistTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistUser_Artists_ArtistId",
                table: "ArtistUser");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistUser_Users_UserId",
                table: "ArtistUser");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Episodes_EpisodeId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Movies_MovieId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_People_PersonId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Roles_RoleId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Seasons_SeasonId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Tvs_TvId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationMovie_Certifications_CertificationId",
                table: "CertificationMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationMovie_Movies_MovieId",
                table: "CertificationMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationTv_Certifications_CertificationId",
                table: "CertificationTv");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationTv_Tvs_TvId",
                table: "CertificationTv");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionLibrary_Collections_CollectionId",
                table: "CollectionLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionLibrary_Libraries_LibraryId",
                table: "CollectionLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionMovie_Collections_CollectionId",
                table: "CollectionMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionMovie_Movies_MovieId",
                table: "CollectionMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Libraries_LibraryId",
                table: "Collections");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionUser_Collections_CollectionId",
                table: "CollectionUser");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionUser_Users_UserId",
                table: "CollectionUser");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyMovie_Companies_CompanyId",
                table: "CompanyMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyMovie_Movies_MovieId",
                table: "CompanyMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyTv_Companies_CompanyId",
                table: "CompanyTv");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyTv_Tvs_TvId",
                table: "CompanyTv");

            migrationBuilder.DropForeignKey(
                name: "FK_Creators_People_PersonId",
                table: "Creators");

            migrationBuilder.DropForeignKey(
                name: "FK_Creators_Tvs_TvId",
                table: "Creators");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Episodes_EpisodeId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Jobs_JobId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Movies_MovieId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_People_PersonId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Seasons_SeasonId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Tvs_TvId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_EncoderProfileFolder_EncoderProfiles_EncoderProfileId",
                table: "EncoderProfileFolder");

            migrationBuilder.DropForeignKey(
                name: "FK_EncoderProfileFolder_Folders_FolderId",
                table: "EncoderProfileFolder");

            migrationBuilder.DropForeignKey(
                name: "FK_Episodes_Seasons_SeasonId",
                table: "Episodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Episodes_Tvs_TvId",
                table: "Episodes");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderLibrary_Folders_FolderId",
                table: "FolderLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderLibrary_Libraries_LibraryId",
                table: "FolderLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreMovie_Genres_GenreId",
                table: "GenreMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreMovie_Movies_MovieId",
                table: "GenreMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreTv_Genres_GenreId",
                table: "GenreTv");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreTv_Tvs_TvId",
                table: "GenreTv");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestStars_Episodes_EpisodeId",
                table: "GuestStars");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestStars_People_PersonId",
                table: "GuestStars");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Albums_AlbumId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Artists_ArtistId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Casts_CastId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Collections_CollectionId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Crews_CrewId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Episodes_EpisodeId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Movies_MovieId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_People_PersonId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Seasons_SeasonId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Tracks_TrackId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Tvs_TvId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordMovie_Keywords_KeywordId",
                table: "KeywordMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordMovie_Movies_MovieId",
                table: "KeywordMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTv_Keywords_KeywordId",
                table: "KeywordTv");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTv_Tvs_TvId",
                table: "KeywordTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LanguageLibrary_Languages_LanguageId",
                table: "LanguageLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_LanguageLibrary_Libraries_LibraryId",
                table: "LanguageLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryMovie_Libraries_LibraryId",
                table: "LibraryMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryMovie_Movies_MovieId",
                table: "LibraryMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTrack_Libraries_LibraryId",
                table: "LibraryTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTrack_Tracks_TrackId",
                table: "LibraryTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTv_Libraries_LibraryId",
                table: "LibraryTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTv_Tvs_TvId",
                table: "LibraryTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryUser_Libraries_LibraryId",
                table: "LibraryUser");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryUser_Users_UserId",
                table: "LibraryUser");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Episodes_EpisodeId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Movies_MovieId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_People_PersonId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Seasons_SeasonId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Tvs_TvId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_VideoFiles_VideoFileId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Libraries_LibraryId",
                table: "Movies");

            migrationBuilder.DropForeignKey(
                name: "FK_MovieUser_Movies_MovieId",
                table: "MovieUser");

            migrationBuilder.DropForeignKey(
                name: "FK_MovieUser_Users_UserId",
                table: "MovieUser");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicGenreReleaseGroup_MusicGenres_GenreId",
                table: "MusicGenreReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicGenreReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "MusicGenreReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicGenreTrack_MusicGenres_GenreId",
                table: "MusicGenreTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicGenreTrack_Tracks_TrackId",
                table: "MusicGenreTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicPlays_Tracks_TrackId",
                table: "MusicPlays");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicPlays_Users_UserId",
                table: "MusicPlays");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkTv_Networks_NetworkId",
                table: "NetworkTv");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkTv_Tvs_TvId",
                table: "NetworkTv");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationUser_Notifications_NotificationId",
                table: "NotificationUser");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationUser_Users_UserId",
                table: "NotificationUser");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Collections_CollectionId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Libraries_LibraryId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Movies_MovieId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Specials_SpecialId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Tvs_TvId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Users_UserId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_Playlists_Users_UserId",
                table: "Playlists");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistTrack_Playlists_PlaylistId",
                table: "PlaylistTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistTrack_Tracks_TrackId",
                table: "PlaylistTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Movies_MovieFromId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Movies_MovieToId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Tvs_TvFromId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Tvs_TvToId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseGroups_Libraries_LibraryId",
                table: "ReleaseGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_GuestStars_GuestStarId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Movies_MovieId",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Tvs_TvId",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Movies_MovieFromId",
                table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Movies_MovieToId",
                table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Tvs_TvFromId",
                table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Tvs_TvToId",
                table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Episodes_EpisodeId",
                table: "SpecialItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Movies_MovieId",
                table: "SpecialItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Specials_SpecialId",
                table: "SpecialItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialUser_Specials_SpecialId",
                table: "SpecialUser");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialUser_Users_UserId",
                table: "SpecialUser");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Folders_FolderId",
                table: "Tracks");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackUser_Tracks_TrackId",
                table: "TrackUser");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackUser_Users_UserId",
                table: "TrackUser");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Albums_AlbumId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Artists_ArtistId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Collections_CollectionId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Episodes_EpisodeId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Genres_GenreId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Movies_MovieId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_People_PersonId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_ReleaseGroups_ReleaseGroupId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Seasons_SeasonId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Tvs_TvId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Tvs_Libraries_LibraryId",
                table: "Tvs");

            migrationBuilder.DropForeignKey(
                name: "FK_TvUser_Tvs_TvId",
                table: "TvUser");

            migrationBuilder.DropForeignKey(
                name: "FK_TvUser_Users_UserId",
                table: "TvUser");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Collections_CollectionId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Movies_MovieId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_SpecialItems_SpecialItemId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Specials_SpecialId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Tvs_TvId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Users_UserId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_VideoFiles_VideoFileId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Episodes_EpisodeId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Metadata_MetadataId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Movies_MovieId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_Movies_MovieId",
                table: "WatchProviderMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_Tvs_TvId",
                table: "WatchProviderMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_WatchProviders_WatchProviderId",
                table: "WatchProviderMedia");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Devices_DeviceId",
                table: "ActivityLogs",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Users_UserId",
                table: "ActivityLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtist_Albums_AlbumId",
                table: "AlbumArtist",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtist_Artists_ArtistId",
                table: "AlbumArtist",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumLibrary_Albums_AlbumId",
                table: "AlbumLibrary",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumLibrary_Libraries_LibraryId",
                table: "AlbumLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumMusicGenre_Albums_AlbumId",
                table: "AlbumMusicGenre",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumMusicGenre_MusicGenres_MusicGenreId",
                table: "AlbumMusicGenre",
                column: "MusicGenreId",
                principalTable: "MusicGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumReleaseGroup_Albums_AlbumId",
                table: "AlbumReleaseGroup",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "AlbumReleaseGroup",
                column: "ReleaseGroupId",
                principalTable: "ReleaseGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Folders_FolderId",
                table: "Albums",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Libraries_LibraryId",
                table: "Albums",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Metadata_MetadataId",
                table: "Albums",
                column: "MetadataId",
                principalTable: "Metadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumTrack_Albums_AlbumId",
                table: "AlbumTrack",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumTrack_Tracks_TrackId",
                table: "AlbumTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumUser_Albums_AlbumId",
                table: "AlbumUser",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumUser_Users_UserId",
                table: "AlbumUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlternativeTitles_Movies_MovieId",
                table: "AlternativeTitles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlternativeTitles_Tvs_TvId",
                table: "AlternativeTitles",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistLibrary_Artists_ArtistId",
                table: "ArtistLibrary",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistLibrary_Libraries_LibraryId",
                table: "ArtistLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistMusicGenre_Artists_ArtistId",
                table: "ArtistMusicGenre",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistMusicGenre_MusicGenres_MusicGenreId",
                table: "ArtistMusicGenre",
                column: "MusicGenreId",
                principalTable: "MusicGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistReleaseGroup_Artists_ArtistId",
                table: "ArtistReleaseGroup",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "ArtistReleaseGroup",
                column: "ReleaseGroupId",
                principalTable: "ReleaseGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Artists_Folders_FolderId",
                table: "Artists",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Artists_Libraries_LibraryId",
                table: "Artists",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistTrack_Artists_ArtistId",
                table: "ArtistTrack",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistTrack_Tracks_TrackId",
                table: "ArtistTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistUser_Artists_ArtistId",
                table: "ArtistUser",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistUser_Users_UserId",
                table: "ArtistUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Episodes_EpisodeId",
                table: "Casts",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Movies_MovieId",
                table: "Casts",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_People_PersonId",
                table: "Casts",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Roles_RoleId",
                table: "Casts",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Seasons_SeasonId",
                table: "Casts",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Tvs_TvId",
                table: "Casts",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationMovie_Certifications_CertificationId",
                table: "CertificationMovie",
                column: "CertificationId",
                principalTable: "Certifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationMovie_Movies_MovieId",
                table: "CertificationMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationTv_Certifications_CertificationId",
                table: "CertificationTv",
                column: "CertificationId",
                principalTable: "Certifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationTv_Tvs_TvId",
                table: "CertificationTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionLibrary_Collections_CollectionId",
                table: "CollectionLibrary",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionLibrary_Libraries_LibraryId",
                table: "CollectionLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionMovie_Collections_CollectionId",
                table: "CollectionMovie",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionMovie_Movies_MovieId",
                table: "CollectionMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Libraries_LibraryId",
                table: "Collections",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionUser_Collections_CollectionId",
                table: "CollectionUser",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionUser_Users_UserId",
                table: "CollectionUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyMovie_Companies_CompanyId",
                table: "CompanyMovie",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyMovie_Movies_MovieId",
                table: "CompanyMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyTv_Companies_CompanyId",
                table: "CompanyTv",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyTv_Tvs_TvId",
                table: "CompanyTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Creators_People_PersonId",
                table: "Creators",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Creators_Tvs_TvId",
                table: "Creators",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Episodes_EpisodeId",
                table: "Crews",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Jobs_JobId",
                table: "Crews",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Movies_MovieId",
                table: "Crews",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_People_PersonId",
                table: "Crews",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Seasons_SeasonId",
                table: "Crews",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Tvs_TvId",
                table: "Crews",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EncoderProfileFolder_EncoderProfiles_EncoderProfileId",
                table: "EncoderProfileFolder",
                column: "EncoderProfileId",
                principalTable: "EncoderProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EncoderProfileFolder_Folders_FolderId",
                table: "EncoderProfileFolder",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Episodes_Seasons_SeasonId",
                table: "Episodes",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Episodes_Tvs_TvId",
                table: "Episodes",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderLibrary_Folders_FolderId",
                table: "FolderLibrary",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderLibrary_Libraries_LibraryId",
                table: "FolderLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreMovie_Genres_GenreId",
                table: "GenreMovie",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreMovie_Movies_MovieId",
                table: "GenreMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreTv_Genres_GenreId",
                table: "GenreTv",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreTv_Tvs_TvId",
                table: "GenreTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestStars_Episodes_EpisodeId",
                table: "GuestStars",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestStars_People_PersonId",
                table: "GuestStars",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Albums_AlbumId",
                table: "Images",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Artists_ArtistId",
                table: "Images",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Casts_CastId",
                table: "Images",
                column: "CastId",
                principalTable: "Casts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Collections_CollectionId",
                table: "Images",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Crews_CrewId",
                table: "Images",
                column: "CrewId",
                principalTable: "Crews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Episodes_EpisodeId",
                table: "Images",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Movies_MovieId",
                table: "Images",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_People_PersonId",
                table: "Images",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Seasons_SeasonId",
                table: "Images",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Tracks_TrackId",
                table: "Images",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Tvs_TvId",
                table: "Images",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordMovie_Keywords_KeywordId",
                table: "KeywordMovie",
                column: "KeywordId",
                principalTable: "Keywords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordMovie_Movies_MovieId",
                table: "KeywordMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTv_Keywords_KeywordId",
                table: "KeywordTv",
                column: "KeywordId",
                principalTable: "Keywords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTv_Tvs_TvId",
                table: "KeywordTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LanguageLibrary_Languages_LanguageId",
                table: "LanguageLibrary",
                column: "LanguageId",
                principalTable: "Languages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LanguageLibrary_Libraries_LibraryId",
                table: "LanguageLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryMovie_Libraries_LibraryId",
                table: "LibraryMovie",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryMovie_Movies_MovieId",
                table: "LibraryMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTrack_Libraries_LibraryId",
                table: "LibraryTrack",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTrack_Tracks_TrackId",
                table: "LibraryTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTv_Libraries_LibraryId",
                table: "LibraryTv",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTv_Tvs_TvId",
                table: "LibraryTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryUser_Libraries_LibraryId",
                table: "LibraryUser",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryUser_Users_UserId",
                table: "LibraryUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Episodes_EpisodeId",
                table: "Medias",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Movies_MovieId",
                table: "Medias",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_People_PersonId",
                table: "Medias",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Seasons_SeasonId",
                table: "Medias",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Tvs_TvId",
                table: "Medias",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_VideoFiles_VideoFileId",
                table: "Medias",
                column: "VideoFileId",
                principalTable: "VideoFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Libraries_LibraryId",
                table: "Movies",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovieUser_Movies_MovieId",
                table: "MovieUser",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovieUser_Users_UserId",
                table: "MovieUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicGenreReleaseGroup_MusicGenres_GenreId",
                table: "MusicGenreReleaseGroup",
                column: "GenreId",
                principalTable: "MusicGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicGenreReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "MusicGenreReleaseGroup",
                column: "ReleaseGroupId",
                principalTable: "ReleaseGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicGenreTrack_MusicGenres_GenreId",
                table: "MusicGenreTrack",
                column: "GenreId",
                principalTable: "MusicGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicGenreTrack_Tracks_TrackId",
                table: "MusicGenreTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicPlays_Tracks_TrackId",
                table: "MusicPlays",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicPlays_Users_UserId",
                table: "MusicPlays",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkTv_Networks_NetworkId",
                table: "NetworkTv",
                column: "NetworkId",
                principalTable: "Networks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkTv_Tvs_TvId",
                table: "NetworkTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationUser_Notifications_NotificationId",
                table: "NotificationUser",
                column: "NotificationId",
                principalTable: "Notifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationUser_Users_UserId",
                table: "NotificationUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Collections_CollectionId",
                table: "PlaybackPreferences",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Libraries_LibraryId",
                table: "PlaybackPreferences",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Movies_MovieId",
                table: "PlaybackPreferences",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Specials_SpecialId",
                table: "PlaybackPreferences",
                column: "SpecialId",
                principalTable: "Specials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Tvs_TvId",
                table: "PlaybackPreferences",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Users_UserId",
                table: "PlaybackPreferences",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Playlists_Users_UserId",
                table: "Playlists",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistTrack_Playlists_PlaylistId",
                table: "PlaylistTrack",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistTrack_Tracks_TrackId",
                table: "PlaylistTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Movies_MovieFromId",
                table: "Recommendations",
                column: "MovieFromId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Movies_MovieToId",
                table: "Recommendations",
                column: "MovieToId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Tvs_TvFromId",
                table: "Recommendations",
                column: "TvFromId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Tvs_TvToId",
                table: "Recommendations",
                column: "TvToId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseGroups_Libraries_LibraryId",
                table: "ReleaseGroups",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_GuestStars_GuestStarId",
                table: "Roles",
                column: "GuestStarId",
                principalTable: "GuestStars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Movies_MovieId",
                table: "Seasons",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Tvs_TvId",
                table: "Seasons",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Movies_MovieFromId",
                table: "Similar",
                column: "MovieFromId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Movies_MovieToId",
                table: "Similar",
                column: "MovieToId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Tvs_TvFromId",
                table: "Similar",
                column: "TvFromId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Tvs_TvToId",
                table: "Similar",
                column: "TvToId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Episodes_EpisodeId",
                table: "SpecialItems",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Movies_MovieId",
                table: "SpecialItems",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Specials_SpecialId",
                table: "SpecialItems",
                column: "SpecialId",
                principalTable: "Specials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialUser_Specials_SpecialId",
                table: "SpecialUser",
                column: "SpecialId",
                principalTable: "Specials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialUser_Users_UserId",
                table: "SpecialUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Folders_FolderId",
                table: "Tracks",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackUser_Tracks_TrackId",
                table: "TrackUser",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackUser_Users_UserId",
                table: "TrackUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Albums_AlbumId",
                table: "Translations",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Artists_ArtistId",
                table: "Translations",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Collections_CollectionId",
                table: "Translations",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Episodes_EpisodeId",
                table: "Translations",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Genres_GenreId",
                table: "Translations",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Movies_MovieId",
                table: "Translations",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_People_PersonId",
                table: "Translations",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_ReleaseGroups_ReleaseGroupId",
                table: "Translations",
                column: "ReleaseGroupId",
                principalTable: "ReleaseGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Seasons_SeasonId",
                table: "Translations",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Tvs_TvId",
                table: "Translations",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tvs_Libraries_LibraryId",
                table: "Tvs",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TvUser_Tvs_TvId",
                table: "TvUser",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TvUser_Users_UserId",
                table: "TvUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Collections_CollectionId",
                table: "UserData",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Movies_MovieId",
                table: "UserData",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_SpecialItems_SpecialItemId",
                table: "UserData",
                column: "SpecialItemId",
                principalTable: "SpecialItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Specials_SpecialId",
                table: "UserData",
                column: "SpecialId",
                principalTable: "Specials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Tvs_TvId",
                table: "UserData",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Users_UserId",
                table: "UserData",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_VideoFiles_VideoFileId",
                table: "UserData",
                column: "VideoFileId",
                principalTable: "VideoFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Episodes_EpisodeId",
                table: "VideoFiles",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Metadata_MetadataId",
                table: "VideoFiles",
                column: "MetadataId",
                principalTable: "Metadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Movies_MovieId",
                table: "VideoFiles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_Movies_MovieId",
                table: "WatchProviderMedia",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_Tvs_TvId",
                table: "WatchProviderMedia",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_WatchProviders_WatchProviderId",
                table: "WatchProviderMedia",
                column: "WatchProviderId",
                principalTable: "WatchProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Devices_DeviceId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Users_UserId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtist_Albums_AlbumId",
                table: "AlbumArtist");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtist_Artists_ArtistId",
                table: "AlbumArtist");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumLibrary_Albums_AlbumId",
                table: "AlbumLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumLibrary_Libraries_LibraryId",
                table: "AlbumLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumMusicGenre_Albums_AlbumId",
                table: "AlbumMusicGenre");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumMusicGenre_MusicGenres_MusicGenreId",
                table: "AlbumMusicGenre");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumReleaseGroup_Albums_AlbumId",
                table: "AlbumReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "AlbumReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Folders_FolderId",
                table: "Albums");

            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Libraries_LibraryId",
                table: "Albums");

            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Metadata_MetadataId",
                table: "Albums");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumTrack_Albums_AlbumId",
                table: "AlbumTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumTrack_Tracks_TrackId",
                table: "AlbumTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumUser_Albums_AlbumId",
                table: "AlbumUser");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumUser_Users_UserId",
                table: "AlbumUser");

            migrationBuilder.DropForeignKey(
                name: "FK_AlternativeTitles_Movies_MovieId",
                table: "AlternativeTitles");

            migrationBuilder.DropForeignKey(
                name: "FK_AlternativeTitles_Tvs_TvId",
                table: "AlternativeTitles");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistLibrary_Artists_ArtistId",
                table: "ArtistLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistLibrary_Libraries_LibraryId",
                table: "ArtistLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistMusicGenre_Artists_ArtistId",
                table: "ArtistMusicGenre");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistMusicGenre_MusicGenres_MusicGenreId",
                table: "ArtistMusicGenre");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistReleaseGroup_Artists_ArtistId",
                table: "ArtistReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "ArtistReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_Artists_Folders_FolderId",
                table: "Artists");

            migrationBuilder.DropForeignKey(
                name: "FK_Artists_Libraries_LibraryId",
                table: "Artists");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistTrack_Artists_ArtistId",
                table: "ArtistTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistTrack_Tracks_TrackId",
                table: "ArtistTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistUser_Artists_ArtistId",
                table: "ArtistUser");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistUser_Users_UserId",
                table: "ArtistUser");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Episodes_EpisodeId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Movies_MovieId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_People_PersonId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Roles_RoleId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Seasons_SeasonId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_Casts_Tvs_TvId",
                table: "Casts");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationMovie_Certifications_CertificationId",
                table: "CertificationMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationMovie_Movies_MovieId",
                table: "CertificationMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationTv_Certifications_CertificationId",
                table: "CertificationTv");

            migrationBuilder.DropForeignKey(
                name: "FK_CertificationTv_Tvs_TvId",
                table: "CertificationTv");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionLibrary_Collections_CollectionId",
                table: "CollectionLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionLibrary_Libraries_LibraryId",
                table: "CollectionLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionMovie_Collections_CollectionId",
                table: "CollectionMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionMovie_Movies_MovieId",
                table: "CollectionMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Libraries_LibraryId",
                table: "Collections");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionUser_Collections_CollectionId",
                table: "CollectionUser");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionUser_Users_UserId",
                table: "CollectionUser");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyMovie_Companies_CompanyId",
                table: "CompanyMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyMovie_Movies_MovieId",
                table: "CompanyMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyTv_Companies_CompanyId",
                table: "CompanyTv");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyTv_Tvs_TvId",
                table: "CompanyTv");

            migrationBuilder.DropForeignKey(
                name: "FK_Creators_People_PersonId",
                table: "Creators");

            migrationBuilder.DropForeignKey(
                name: "FK_Creators_Tvs_TvId",
                table: "Creators");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Episodes_EpisodeId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Jobs_JobId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Movies_MovieId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_People_PersonId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Seasons_SeasonId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_Crews_Tvs_TvId",
                table: "Crews");

            migrationBuilder.DropForeignKey(
                name: "FK_EncoderProfileFolder_EncoderProfiles_EncoderProfileId",
                table: "EncoderProfileFolder");

            migrationBuilder.DropForeignKey(
                name: "FK_EncoderProfileFolder_Folders_FolderId",
                table: "EncoderProfileFolder");

            migrationBuilder.DropForeignKey(
                name: "FK_Episodes_Seasons_SeasonId",
                table: "Episodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Episodes_Tvs_TvId",
                table: "Episodes");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderLibrary_Folders_FolderId",
                table: "FolderLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_FolderLibrary_Libraries_LibraryId",
                table: "FolderLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreMovie_Genres_GenreId",
                table: "GenreMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreMovie_Movies_MovieId",
                table: "GenreMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreTv_Genres_GenreId",
                table: "GenreTv");

            migrationBuilder.DropForeignKey(
                name: "FK_GenreTv_Tvs_TvId",
                table: "GenreTv");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestStars_Episodes_EpisodeId",
                table: "GuestStars");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestStars_People_PersonId",
                table: "GuestStars");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Albums_AlbumId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Artists_ArtistId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Casts_CastId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Collections_CollectionId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Crews_CrewId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Episodes_EpisodeId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Movies_MovieId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_People_PersonId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Seasons_SeasonId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Tracks_TrackId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Tvs_TvId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordMovie_Keywords_KeywordId",
                table: "KeywordMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordMovie_Movies_MovieId",
                table: "KeywordMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTv_Keywords_KeywordId",
                table: "KeywordTv");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTv_Tvs_TvId",
                table: "KeywordTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LanguageLibrary_Languages_LanguageId",
                table: "LanguageLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_LanguageLibrary_Libraries_LibraryId",
                table: "LanguageLibrary");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryMovie_Libraries_LibraryId",
                table: "LibraryMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryMovie_Movies_MovieId",
                table: "LibraryMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTrack_Libraries_LibraryId",
                table: "LibraryTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTrack_Tracks_TrackId",
                table: "LibraryTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTv_Libraries_LibraryId",
                table: "LibraryTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryTv_Tvs_TvId",
                table: "LibraryTv");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryUser_Libraries_LibraryId",
                table: "LibraryUser");

            migrationBuilder.DropForeignKey(
                name: "FK_LibraryUser_Users_UserId",
                table: "LibraryUser");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Episodes_EpisodeId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Movies_MovieId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_People_PersonId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Seasons_SeasonId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Tvs_TvId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_VideoFiles_VideoFileId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Libraries_LibraryId",
                table: "Movies");

            migrationBuilder.DropForeignKey(
                name: "FK_MovieUser_Movies_MovieId",
                table: "MovieUser");

            migrationBuilder.DropForeignKey(
                name: "FK_MovieUser_Users_UserId",
                table: "MovieUser");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicGenreReleaseGroup_MusicGenres_GenreId",
                table: "MusicGenreReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicGenreReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "MusicGenreReleaseGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicGenreTrack_MusicGenres_GenreId",
                table: "MusicGenreTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicGenreTrack_Tracks_TrackId",
                table: "MusicGenreTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicPlays_Tracks_TrackId",
                table: "MusicPlays");

            migrationBuilder.DropForeignKey(
                name: "FK_MusicPlays_Users_UserId",
                table: "MusicPlays");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkTv_Networks_NetworkId",
                table: "NetworkTv");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkTv_Tvs_TvId",
                table: "NetworkTv");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationUser_Notifications_NotificationId",
                table: "NotificationUser");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationUser_Users_UserId",
                table: "NotificationUser");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Collections_CollectionId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Libraries_LibraryId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Movies_MovieId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Specials_SpecialId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Tvs_TvId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackPreferences_Users_UserId",
                table: "PlaybackPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_Playlists_Users_UserId",
                table: "Playlists");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistTrack_Playlists_PlaylistId",
                table: "PlaylistTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistTrack_Tracks_TrackId",
                table: "PlaylistTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Movies_MovieFromId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Movies_MovieToId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Tvs_TvFromId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Tvs_TvToId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseGroups_Libraries_LibraryId",
                table: "ReleaseGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_GuestStars_GuestStarId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Movies_MovieId",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Tvs_TvId",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Movies_MovieFromId",
                table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Movies_MovieToId",
                table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Tvs_TvFromId",
                table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_Similar_Tvs_TvToId",
                table: "Similar");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Episodes_EpisodeId",
                table: "SpecialItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Movies_MovieId",
                table: "SpecialItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialItems_Specials_SpecialId",
                table: "SpecialItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialUser_Specials_SpecialId",
                table: "SpecialUser");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialUser_Users_UserId",
                table: "SpecialUser");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Folders_FolderId",
                table: "Tracks");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackUser_Tracks_TrackId",
                table: "TrackUser");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackUser_Users_UserId",
                table: "TrackUser");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Albums_AlbumId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Artists_ArtistId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Collections_CollectionId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Episodes_EpisodeId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Genres_GenreId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Movies_MovieId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_People_PersonId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_ReleaseGroups_ReleaseGroupId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Seasons_SeasonId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Translations_Tvs_TvId",
                table: "Translations");

            migrationBuilder.DropForeignKey(
                name: "FK_Tvs_Libraries_LibraryId",
                table: "Tvs");

            migrationBuilder.DropForeignKey(
                name: "FK_TvUser_Tvs_TvId",
                table: "TvUser");

            migrationBuilder.DropForeignKey(
                name: "FK_TvUser_Users_UserId",
                table: "TvUser");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Collections_CollectionId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Movies_MovieId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_SpecialItems_SpecialItemId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Specials_SpecialId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Tvs_TvId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_Users_UserId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_UserData_VideoFiles_VideoFileId",
                table: "UserData");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Episodes_EpisodeId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Metadata_MetadataId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Movies_MovieId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_Movies_MovieId",
                table: "WatchProviderMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_Tvs_TvId",
                table: "WatchProviderMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProviderMedia_WatchProviders_WatchProviderId",
                table: "WatchProviderMedia");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Devices_DeviceId",
                table: "ActivityLogs",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Users_UserId",
                table: "ActivityLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtist_Albums_AlbumId",
                table: "AlbumArtist",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtist_Artists_ArtistId",
                table: "AlbumArtist",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumLibrary_Albums_AlbumId",
                table: "AlbumLibrary",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumLibrary_Libraries_LibraryId",
                table: "AlbumLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumMusicGenre_Albums_AlbumId",
                table: "AlbumMusicGenre",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumMusicGenre_MusicGenres_MusicGenreId",
                table: "AlbumMusicGenre",
                column: "MusicGenreId",
                principalTable: "MusicGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumReleaseGroup_Albums_AlbumId",
                table: "AlbumReleaseGroup",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "AlbumReleaseGroup",
                column: "ReleaseGroupId",
                principalTable: "ReleaseGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Folders_FolderId",
                table: "Albums",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Libraries_LibraryId",
                table: "Albums",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Metadata_MetadataId",
                table: "Albums",
                column: "MetadataId",
                principalTable: "Metadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumTrack_Albums_AlbumId",
                table: "AlbumTrack",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumTrack_Tracks_TrackId",
                table: "AlbumTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumUser_Albums_AlbumId",
                table: "AlbumUser",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumUser_Users_UserId",
                table: "AlbumUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlternativeTitles_Movies_MovieId",
                table: "AlternativeTitles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlternativeTitles_Tvs_TvId",
                table: "AlternativeTitles",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistLibrary_Artists_ArtistId",
                table: "ArtistLibrary",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistLibrary_Libraries_LibraryId",
                table: "ArtistLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistMusicGenre_Artists_ArtistId",
                table: "ArtistMusicGenre",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistMusicGenre_MusicGenres_MusicGenreId",
                table: "ArtistMusicGenre",
                column: "MusicGenreId",
                principalTable: "MusicGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistReleaseGroup_Artists_ArtistId",
                table: "ArtistReleaseGroup",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "ArtistReleaseGroup",
                column: "ReleaseGroupId",
                principalTable: "ReleaseGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Artists_Folders_FolderId",
                table: "Artists",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Artists_Libraries_LibraryId",
                table: "Artists",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistTrack_Artists_ArtistId",
                table: "ArtistTrack",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistTrack_Tracks_TrackId",
                table: "ArtistTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistUser_Artists_ArtistId",
                table: "ArtistUser",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistUser_Users_UserId",
                table: "ArtistUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Episodes_EpisodeId",
                table: "Casts",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Movies_MovieId",
                table: "Casts",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_People_PersonId",
                table: "Casts",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Roles_RoleId",
                table: "Casts",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Seasons_SeasonId",
                table: "Casts",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Casts_Tvs_TvId",
                table: "Casts",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationMovie_Certifications_CertificationId",
                table: "CertificationMovie",
                column: "CertificationId",
                principalTable: "Certifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationMovie_Movies_MovieId",
                table: "CertificationMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationTv_Certifications_CertificationId",
                table: "CertificationTv",
                column: "CertificationId",
                principalTable: "Certifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CertificationTv_Tvs_TvId",
                table: "CertificationTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionLibrary_Collections_CollectionId",
                table: "CollectionLibrary",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionLibrary_Libraries_LibraryId",
                table: "CollectionLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionMovie_Collections_CollectionId",
                table: "CollectionMovie",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionMovie_Movies_MovieId",
                table: "CollectionMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Libraries_LibraryId",
                table: "Collections",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionUser_Collections_CollectionId",
                table: "CollectionUser",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionUser_Users_UserId",
                table: "CollectionUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyMovie_Companies_CompanyId",
                table: "CompanyMovie",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyMovie_Movies_MovieId",
                table: "CompanyMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyTv_Companies_CompanyId",
                table: "CompanyTv",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyTv_Tvs_TvId",
                table: "CompanyTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Creators_People_PersonId",
                table: "Creators",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Creators_Tvs_TvId",
                table: "Creators",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Episodes_EpisodeId",
                table: "Crews",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Jobs_JobId",
                table: "Crews",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Movies_MovieId",
                table: "Crews",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_People_PersonId",
                table: "Crews",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Seasons_SeasonId",
                table: "Crews",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Crews_Tvs_TvId",
                table: "Crews",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EncoderProfileFolder_EncoderProfiles_EncoderProfileId",
                table: "EncoderProfileFolder",
                column: "EncoderProfileId",
                principalTable: "EncoderProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EncoderProfileFolder_Folders_FolderId",
                table: "EncoderProfileFolder",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Episodes_Seasons_SeasonId",
                table: "Episodes",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Episodes_Tvs_TvId",
                table: "Episodes",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderLibrary_Folders_FolderId",
                table: "FolderLibrary",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FolderLibrary_Libraries_LibraryId",
                table: "FolderLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreMovie_Genres_GenreId",
                table: "GenreMovie",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreMovie_Movies_MovieId",
                table: "GenreMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreTv_Genres_GenreId",
                table: "GenreTv",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GenreTv_Tvs_TvId",
                table: "GenreTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestStars_Episodes_EpisodeId",
                table: "GuestStars",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestStars_People_PersonId",
                table: "GuestStars",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Albums_AlbumId",
                table: "Images",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Artists_ArtistId",
                table: "Images",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Casts_CastId",
                table: "Images",
                column: "CastId",
                principalTable: "Casts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Collections_CollectionId",
                table: "Images",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Crews_CrewId",
                table: "Images",
                column: "CrewId",
                principalTable: "Crews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Episodes_EpisodeId",
                table: "Images",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Movies_MovieId",
                table: "Images",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_People_PersonId",
                table: "Images",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Seasons_SeasonId",
                table: "Images",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Tracks_TrackId",
                table: "Images",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Tvs_TvId",
                table: "Images",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordMovie_Keywords_KeywordId",
                table: "KeywordMovie",
                column: "KeywordId",
                principalTable: "Keywords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordMovie_Movies_MovieId",
                table: "KeywordMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTv_Keywords_KeywordId",
                table: "KeywordTv",
                column: "KeywordId",
                principalTable: "Keywords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTv_Tvs_TvId",
                table: "KeywordTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LanguageLibrary_Languages_LanguageId",
                table: "LanguageLibrary",
                column: "LanguageId",
                principalTable: "Languages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LanguageLibrary_Libraries_LibraryId",
                table: "LanguageLibrary",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryMovie_Libraries_LibraryId",
                table: "LibraryMovie",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryMovie_Movies_MovieId",
                table: "LibraryMovie",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTrack_Libraries_LibraryId",
                table: "LibraryTrack",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTrack_Tracks_TrackId",
                table: "LibraryTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTv_Libraries_LibraryId",
                table: "LibraryTv",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryTv_Tvs_TvId",
                table: "LibraryTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryUser_Libraries_LibraryId",
                table: "LibraryUser",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryUser_Users_UserId",
                table: "LibraryUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Episodes_EpisodeId",
                table: "Medias",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Movies_MovieId",
                table: "Medias",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_People_PersonId",
                table: "Medias",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Seasons_SeasonId",
                table: "Medias",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Tvs_TvId",
                table: "Medias",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_VideoFiles_VideoFileId",
                table: "Medias",
                column: "VideoFileId",
                principalTable: "VideoFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Libraries_LibraryId",
                table: "Movies",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MovieUser_Movies_MovieId",
                table: "MovieUser",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MovieUser_Users_UserId",
                table: "MovieUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicGenreReleaseGroup_MusicGenres_GenreId",
                table: "MusicGenreReleaseGroup",
                column: "GenreId",
                principalTable: "MusicGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicGenreReleaseGroup_ReleaseGroups_ReleaseGroupId",
                table: "MusicGenreReleaseGroup",
                column: "ReleaseGroupId",
                principalTable: "ReleaseGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicGenreTrack_MusicGenres_GenreId",
                table: "MusicGenreTrack",
                column: "GenreId",
                principalTable: "MusicGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicGenreTrack_Tracks_TrackId",
                table: "MusicGenreTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicPlays_Tracks_TrackId",
                table: "MusicPlays",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MusicPlays_Users_UserId",
                table: "MusicPlays",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkTv_Networks_NetworkId",
                table: "NetworkTv",
                column: "NetworkId",
                principalTable: "Networks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkTv_Tvs_TvId",
                table: "NetworkTv",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationUser_Notifications_NotificationId",
                table: "NotificationUser",
                column: "NotificationId",
                principalTable: "Notifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationUser_Users_UserId",
                table: "NotificationUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Collections_CollectionId",
                table: "PlaybackPreferences",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Libraries_LibraryId",
                table: "PlaybackPreferences",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Movies_MovieId",
                table: "PlaybackPreferences",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Specials_SpecialId",
                table: "PlaybackPreferences",
                column: "SpecialId",
                principalTable: "Specials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Tvs_TvId",
                table: "PlaybackPreferences",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackPreferences_Users_UserId",
                table: "PlaybackPreferences",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Playlists_Users_UserId",
                table: "Playlists",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistTrack_Playlists_PlaylistId",
                table: "PlaylistTrack",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistTrack_Tracks_TrackId",
                table: "PlaylistTrack",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Movies_MovieFromId",
                table: "Recommendations",
                column: "MovieFromId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Movies_MovieToId",
                table: "Recommendations",
                column: "MovieToId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Tvs_TvFromId",
                table: "Recommendations",
                column: "TvFromId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Tvs_TvToId",
                table: "Recommendations",
                column: "TvToId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseGroups_Libraries_LibraryId",
                table: "ReleaseGroups",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_GuestStars_GuestStarId",
                table: "Roles",
                column: "GuestStarId",
                principalTable: "GuestStars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Movies_MovieId",
                table: "Seasons",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Tvs_TvId",
                table: "Seasons",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Movies_MovieFromId",
                table: "Similar",
                column: "MovieFromId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Movies_MovieToId",
                table: "Similar",
                column: "MovieToId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Tvs_TvFromId",
                table: "Similar",
                column: "TvFromId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Similar_Tvs_TvToId",
                table: "Similar",
                column: "TvToId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Episodes_EpisodeId",
                table: "SpecialItems",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Movies_MovieId",
                table: "SpecialItems",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialItems_Specials_SpecialId",
                table: "SpecialItems",
                column: "SpecialId",
                principalTable: "Specials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialUser_Specials_SpecialId",
                table: "SpecialUser",
                column: "SpecialId",
                principalTable: "Specials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialUser_Users_UserId",
                table: "SpecialUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Folders_FolderId",
                table: "Tracks",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackUser_Tracks_TrackId",
                table: "TrackUser",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackUser_Users_UserId",
                table: "TrackUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Albums_AlbumId",
                table: "Translations",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Artists_ArtistId",
                table: "Translations",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Collections_CollectionId",
                table: "Translations",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Episodes_EpisodeId",
                table: "Translations",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Genres_GenreId",
                table: "Translations",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Movies_MovieId",
                table: "Translations",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_People_PersonId",
                table: "Translations",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_ReleaseGroups_ReleaseGroupId",
                table: "Translations",
                column: "ReleaseGroupId",
                principalTable: "ReleaseGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Seasons_SeasonId",
                table: "Translations",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Translations_Tvs_TvId",
                table: "Translations",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tvs_Libraries_LibraryId",
                table: "Tvs",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TvUser_Tvs_TvId",
                table: "TvUser",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TvUser_Users_UserId",
                table: "TvUser",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Collections_CollectionId",
                table: "UserData",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Movies_MovieId",
                table: "UserData",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_SpecialItems_SpecialItemId",
                table: "UserData",
                column: "SpecialItemId",
                principalTable: "SpecialItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Specials_SpecialId",
                table: "UserData",
                column: "SpecialId",
                principalTable: "Specials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Tvs_TvId",
                table: "UserData",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_Users_UserId",
                table: "UserData",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserData_VideoFiles_VideoFileId",
                table: "UserData",
                column: "VideoFileId",
                principalTable: "VideoFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Episodes_EpisodeId",
                table: "VideoFiles",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Metadata_MetadataId",
                table: "VideoFiles",
                column: "MetadataId",
                principalTable: "Metadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Movies_MovieId",
                table: "VideoFiles",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_Movies_MovieId",
                table: "WatchProviderMedia",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_Tvs_TvId",
                table: "WatchProviderMedia",
                column: "TvId",
                principalTable: "Tvs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProviderMedia_WatchProviders_WatchProviderId",
                table: "WatchProviderMedia",
                column: "WatchProviderId",
                principalTable: "WatchProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
