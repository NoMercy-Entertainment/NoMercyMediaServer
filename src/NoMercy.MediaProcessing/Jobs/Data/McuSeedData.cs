// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.NmSystem.Domain;

namespace NoMercy.Data.Data;

public static class McuSeedData
{
    public static readonly Special Special = new()
    {
        Id = Ulid.Parse("01HSBYSE7ZNGN7P586BQJ7W9ZB"),
        Title = "Marvel Cinematic Universe",
        Backdrop = "/clje9xd4v0000d4ef0usufhy9.jpg",
        Poster = "/4Af70wDv1sN8JztUNnvXgae193O.jpg",
        Logo = "/hUzeosd33nzE5MCNsZxCGEKTXaQ.png",
        Overview =
            "Chronological order of the movies and episodes from the Marvel Cinematic Universe in the timeline of the story.",
        Creator = "Stoney_Eagle",
    };

    public static readonly SpecialSeedItem[] McuItems =
    [
        new()
        {
            Index = 1,
            Type = MediaTypes.MovieMediaType,
            Title = "Captain America: The First Avenger",
            Year = 2011,
        },
        new()
        {
            Index = 2,
            Type = MediaTypes.MovieMediaType,
            Title = "Marvel One-Shot: Agent Carter",
            Year = 2013,
        },
        new()
        {
            Index = 3,
            Type = MediaTypes.TvMediaType,
            Title = "Agent Carter",
            Year = 2015,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 4,
            Type = MediaTypes.TvMediaType,
            Title = "Agent Carter",
            Year = 2015,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 5,
            Type = MediaTypes.MovieMediaType,
            Title = "Captain Marvel",
            Year = 2019,
        },
        new()
        {
            Index = 6,
            Type = MediaTypes.MovieMediaType,
            Title = "Iron Man",
            Year = 2008,
        },
        new()
        {
            Index = 7,
            Type = MediaTypes.MovieMediaType,
            Title = "Iron Man 2",
            Year = 2010,
        },
        new()
        {
            Index = 8,
            Type = MediaTypes.MovieMediaType,
            Title = "The Incredible Hulk",
            Year = 2008,
        },
        new()
        {
            Index = 9,
            Type = MediaTypes.MovieMediaType,
            Title = "The Consultant",
            Year = 2011,
        },
        new()
        {
            Index = 10,
            Type = MediaTypes.MovieMediaType,
            Title = "A Funny Thing Happened on the Way to Thor's Hammer",
            Year = 2011,
        },
        new()
        {
            Index = 11,
            Type = MediaTypes.MovieMediaType,
            Title = "Thor",
            Year = 2011,
        },
        new()
        {
            Index = 12,
            Type = MediaTypes.MovieMediaType,
            Title = "The Avengers",
            Year = 2012,
        },
        new()
        {
            Index = 13,
            Type = MediaTypes.MovieMediaType,
            Title = "Item 47",
            Year = 2012,
        },
        new()
        {
            Index = 14,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [1],
            Episodes = [1, 2, 3, 4, 5, 6, 7],
        },
        new()
        {
            Index = 15,
            Type = MediaTypes.MovieMediaType,
            Title = "Thor: The Dark World",
            Year = 2013,
        },
        new()
        {
            Index = 16,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [1],
            Episodes = [8, 9, 10, 11, 12, 13, 14, 15, 16],
        },
        new()
        {
            Index = 17,
            Type = MediaTypes.MovieMediaType,
            Title = "Iron Man 3",
            Year = 2013,
        },
        new()
        {
            Index = 18,
            Type = MediaTypes.MovieMediaType,
            Title = "All Hail the King",
            Year = 2014,
        },
        new()
        {
            Index = 19,
            Type = MediaTypes.MovieMediaType,
            Title = "Captain America: The Winter Soldier",
            Year = 2014,
        },
        new()
        {
            Index = 20,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [1],
            Episodes = [17, 18, 19, 20, 21, 22],
        },
        new()
        {
            Index = 21,
            Type = MediaTypes.MovieMediaType,
            Title = "Guardians of the Galaxy",
            Year = 2014,
        },
        new()
        {
            Index = 22,
            Type = MediaTypes.MovieMediaType,
            Title = "Guardians of the Galaxy Vol 2",
            Year = 2017,
        },
        new()
        {
            Index = 23,
            Type = MediaTypes.TvMediaType,
            Title = "I Am Groot",
            Year = 2022,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 24,
            Type = MediaTypes.TvMediaType,
            Title = "I Am Groot",
            Year = 2022,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 25,
            Type = MediaTypes.TvMediaType,
            Title = "Daredevil",
            Year = 2015,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 26,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [2],
            Episodes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
        },
        new()
        {
            Index = 27,
            Type = MediaTypes.TvMediaType,
            Title = "Jessica Jones",
            Year = 2015,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 28,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [2],
            Episodes = [11, 12, 13, 14, 15, 16, 17, 18, 19],
        },
        new()
        {
            Index = 29,
            Type = MediaTypes.MovieMediaType,
            Title = "Avengers: Age of Ultron",
            Year = 2015,
        },
        new()
        {
            Index = 30,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [2],
            Episodes = [20, 21, 22],
        },
        new()
        {
            Index = 31,
            Type = MediaTypes.TvMediaType,
            Title = "Daredevil",
            Year = 2015,
            Seasons = [2],
            Episodes = [1, 2, 3, 4],
        },
        new()
        {
            Index = 32,
            Type = MediaTypes.TvMediaType,
            Title = "Luke Cage",
            Year = 2016,
            Seasons = [1],
            Episodes = [1, 2, 3, 4],
        },
        new()
        {
            Index = 33,
            Type = MediaTypes.TvMediaType,
            Title = "Daredevil",
            Year = 2015,
            Seasons = [2],
            Episodes = [5, 6, 7, 8, 9, 10, 11],
        },
        new()
        {
            Index = 34,
            Type = MediaTypes.TvMediaType,
            Title = "Luke Cage",
            Year = 2016,
            Seasons = [1],
            Episodes = [5, 6, 7, 8],
        },
        new()
        {
            Index = 35,
            Type = MediaTypes.TvMediaType,
            Title = "Daredevil",
            Year = 2015,
            Seasons = [2],
            Episodes = [12, 13],
        },
        new()
        {
            Index = 36,
            Type = MediaTypes.TvMediaType,
            Title = "Luke Cage",
            Year = 2016,
            Seasons = [1],
            Episodes = [9, 10, 11, 12, 13],
        },
        new()
        {
            Index = 37,
            Type = MediaTypes.MovieMediaType,
            Title = "Ant-Man",
            Year = 2015,
        },
        new()
        {
            Index = 38,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [3],
            Episodes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
        },
        new()
        {
            Index = 39,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [3],
            Episodes = [11, 12, 13, 14, 15, 16, 17, 18, 19],
        },
        new()
        {
            Index = 40,
            Type = MediaTypes.TvMediaType,
            Title = "Iron Fist",
            Year = 2017,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 41,
            Type = MediaTypes.MovieMediaType,
            Title = "Captain America: Civil War",
            Year = 2016,
        },
        new()
        {
            Index = 42,
            Type = MediaTypes.MovieMediaType,
            Title = "Team Thor",
            Year = 2016,
        },
        new()
        {
            Index = 43,
            Type = MediaTypes.MovieMediaType,
            Title = "Team Thor: Part 2",
            Year = 2017,
        },
        new()
        {
            Index = 44,
            Type = MediaTypes.MovieMediaType,
            Title = "Black Widow",
            Year = 2021,
        },
        new()
        {
            Index = 45,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [3],
            Episodes = [20, 21, 22],
        },
        new()
        {
            Index = 46,
            Type = MediaTypes.TvMediaType,
            Title = "The Defenders",
            Year = 2017,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 47,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [4],
            Episodes = [1, 2, 3, 4, 5, 6],
        },
        new()
        {
            Index = 48,
            Type = MediaTypes.MovieMediaType,
            Title = "Doctor Strange",
            Year = 2016,
        },
        new()
        {
            Index = 49,
            Type = MediaTypes.MovieMediaType,
            Title = "Black Panther",
            Year = 2018,
        },
        new()
        {
            Index = 50,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [4],
            Episodes = [7, 8],
        },
        new()
        {
            Index = 51,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD: Slingshot",
            Year = 2016,
            Seasons = [1],
            Episodes = [1, 2, 3, 4, 5, 6],
        },
        new()
        {
            Index = 52,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [4],
            Episodes = [9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22],
        },
        new()
        {
            Index = 53,
            Type = MediaTypes.MovieMediaType,
            Title = "Spider-Man: Homecoming",
            Year = 2017,
        },
        new()
        {
            Index = 54,
            Type = MediaTypes.MovieMediaType,
            Title = "Thor: Ragnarok",
            Year = 2017,
        },
        new()
        {
            Index = 55,
            Type = MediaTypes.MovieMediaType,
            Title = "Team Darryl",
            Year = 2018,
        },
        new()
        {
            Index = 56,
            Type = MediaTypes.TvMediaType,
            Title = "Inhumans",
            Year = 2017,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 57,
            Type = MediaTypes.TvMediaType,
            Title = "The Punisher",
            Year = 2017,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 58,
            Type = MediaTypes.TvMediaType,
            Title = "Runaways",
            Year = 2017,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 59,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [5],
            Episodes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
        },
        new()
        {
            Index = 60,
            Type = MediaTypes.TvMediaType,
            Title = "Jessica Jones",
            Year = 2015,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 61,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [5],
            Episodes = [11, 12, 13, 14, 15, 16, 17, 18],
        },
        new()
        {
            Index = 62,
            Type = MediaTypes.TvMediaType,
            Title = "Cloak & Dagger",
            Year = 2018,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 63,
            Type = MediaTypes.TvMediaType,
            Title = "Cloak & Dagger",
            Year = 2018,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 64,
            Type = MediaTypes.TvMediaType,
            Title = "Luke Cage",
            Year = 2016,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 65,
            Type = MediaTypes.TvMediaType,
            Title = "Iron Fist",
            Year = 2017,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 66,
            Type = MediaTypes.TvMediaType,
            Title = "Daredevil",
            Year = 2015,
            Seasons = [3],
            Episodes = [],
        },
        new()
        {
            Index = 67,
            Type = MediaTypes.TvMediaType,
            Title = "Runaways",
            Year = 2017,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 68,
            Type = MediaTypes.TvMediaType,
            Title = "The Punisher",
            Year = 2017,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 69,
            Type = MediaTypes.TvMediaType,
            Title = "Jessica Jones",
            Year = 2015,
            Seasons = [3],
            Episodes = [],
        },
        new()
        {
            Index = 70,
            Type = MediaTypes.MovieMediaType,
            Title = "Ant-Man and the Wasp",
            Year = 2018,
        },
        new()
        {
            Index = 71,
            Type = MediaTypes.MovieMediaType,
            Title = "Avengers: Infinity War",
            Year = 2018,
        },
        new()
        {
            Index = 72,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [5],
            Episodes = [19, 20, 21, 22],
        },
        new()
        {
            Index = 73,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [6],
            Episodes = [],
        },
        new()
        {
            Index = 74,
            Type = MediaTypes.TvMediaType,
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [7],
            Episodes = [],
        },
        new()
        {
            Index = 75,
            Type = MediaTypes.TvMediaType,
            Title = "Runaways",
            Year = 2017,
            Seasons = [3],
            Episodes = [],
        },
        new()
        {
            Index = 76,
            Type = MediaTypes.MovieMediaType,
            Title = "Avengers: Endgame",
            Year = 2019,
        },
        new()
        {
            Index = 77,
            Type = MediaTypes.TvMediaType,
            Title = "Loki",
            Year = 2021,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 78,
            Type = MediaTypes.TvMediaType,
            Title = "What If...?",
            Year = 2021,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 79,
            Type = MediaTypes.TvMediaType,
            Title = "What If...?",
            Year = 2021,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 80,
            Type = MediaTypes.TvMediaType,
            Title = "WandaVision",
            Year = 2021,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 81,
            Type = MediaTypes.TvMediaType,
            Title = "The Falcon and the Winter Soldier",
            Year = 2021,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 82,
            Type = MediaTypes.MovieMediaType,
            Title = "Shang-Chi and the Legend of the Ten Rings",
            Year = 2021,
        },
        new()
        {
            Index = 83,
            Type = MediaTypes.MovieMediaType,
            Title = "Eternals",
            Year = 2021,
        },
        new()
        {
            Index = 84,
            Type = MediaTypes.MovieMediaType,
            Title = "Spider-Man: Far From Home",
            Year = 2019,
        },
        new()
        {
            Index = 85,
            Type = MediaTypes.MovieMediaType,
            Title = "Spider-Man: No Way Home",
            Year = 2021,
        },
        new()
        {
            Index = 86,
            Type = MediaTypes.MovieMediaType,
            Title = "Doctor Strange in the Multiverse of Madness",
            Year = 2022,
        },
        new()
        {
            Index = 87,
            Type = MediaTypes.TvMediaType,
            Title = "Hawkeye",
            Year = 2021,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 88,
            Type = MediaTypes.TvMediaType,
            Title = "Moon Knight",
            Year = 2022,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 89,
            Type = MediaTypes.MovieMediaType,
            Title = "Black Panther: Wakanda Forever",
            Year = 2022,
        },
        new()
        {
            Index = 90,
            Type = MediaTypes.TvMediaType,
            Title = "Echo",
            Year = 2024,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 91,
            Type = MediaTypes.TvMediaType,
            Title = "She-Hulk: Attorney at Law",
            Year = 2022,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 92,
            Type = MediaTypes.TvMediaType,
            Title = "Ms Marvel",
            Year = 2022,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 93,
            Type = MediaTypes.MovieMediaType,
            Title = "Thor: Love and Thunder",
            Year = 2022,
        },
        new()
        {
            Index = 94,
            Type = MediaTypes.MovieMediaType,
            Title = "Werewolf by Night",
            Year = 2022,
        },
        new()
        {
            Index = 95,
            Type = MediaTypes.MovieMediaType,
            Title = "The Guardians of the Galaxy Holiday Special",
            Year = 2022,
        },
        new()
        {
            Index = 96,
            Type = MediaTypes.MovieMediaType,
            Title = "Ant-Man and The Wasp: Quantumania",
            Year = 2023,
        },
        new()
        {
            Index = 97,
            Type = MediaTypes.MovieMediaType,
            Title = "Guardians of the Galaxy Vol 3",
            Year = 2023,
        },
        new()
        {
            Index = 98,
            Type = MediaTypes.TvMediaType,
            Title = "Secret Invasion",
            Year = 2023,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 99,
            Type = MediaTypes.TvMediaType,
            Title = "Loki",
            Year = 2021,
            Seasons = [2],
            Episodes = [],
        },
        new()
        {
            Index = 100,
            Type = MediaTypes.MovieMediaType,
            Title = "The Marvels",
            Year = 2023,
        },
        new()
        {
            Index = 101,
            Type = MediaTypes.MovieMediaType,
            Title = "Deadpool & Wolverine",
            Year = 2024,
        },
        new()
        {
            Index = 102,
            Type = MediaTypes.TvMediaType,
            Title = "Agatha All Along",
            Year = 2024,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 103,
            Type = MediaTypes.TvMediaType,
            Title = "X-Men '97",
            Year = 2024,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 104,
            Type = MediaTypes.TvMediaType,
            Title = "What If...?",
            Year = 2021,
            Seasons = [3],
            Episodes = [],
        },
        new()
        {
            Index = 105,
            Type = MediaTypes.TvMediaType,
            Title = "Your Friendly Neighborhood Spider-Man",
            Year = 2025,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 106,
            Type = MediaTypes.MovieMediaType,
            Title = "Captain America: Brave New World",
            Year = 2025,
        },
        new()
        {
            Index = 107,
            Type = MediaTypes.TvMediaType,
            Title = "Daredevil: Born Again",
            Year = 2025,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 108,
            Type = MediaTypes.TvMediaType,
            Title = "Ironheart",
            Year = 2025,
            Seasons = [1],
            Episodes = [],
        },
        new()
        {
            Index = 109,
            Type = MediaTypes.MovieMediaType,
            Title = "Thunderbolts*",
            Year = 2025,
        },
        new()
        {
            Index = 110,
            Type = MediaTypes.MovieMediaType,
            Title = "The Fantastic Four: First Steps",
            Year = 2025,
        },
        new()
        {
            Index = 111,
            Type = MediaTypes.TvMediaType,
            Title = "Wonder Man",
            Year = 2026,
            Seasons = [1],
            Episodes = [],
        },
    ];
}
