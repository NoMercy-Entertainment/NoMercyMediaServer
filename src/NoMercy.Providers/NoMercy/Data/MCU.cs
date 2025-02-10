using NoMercy.Providers.NoMercy.Models.Specials;

namespace NoMercy.Providers.NoMercy.Data;

public static class Mcu
{
    public static readonly Special Special = new()
    {
        Id = Ulid.Parse("01HSBYSE7ZNGN7P586BQJ7W9ZB"),
        Title = "Marvel Cinematic Universe",
        Backdrop = "/clje9xd4v0000d4ef0usufhy9.jpg",
        Poster = "/4Af70wDv1sN8JztUNnvXgae193O.jpg",
        Logo = "/hUzeosd33nzE5MCNsZxCGEKTXaQ.png",
        Description =
            "Chronological order of the movies and episodes from the Marvel Cinematic Universe in the timeline of the story.",
        Creator = "Stoney_Eagle"
    };

    public static readonly CollectionItem[] McuItems =
    [
        new()
        {
            Index = 1,
            Type = "movie",
            Title = "Captain America: The First Avenger",
            Year = 2011
        },
        new()
        {
            Index = 2,
            Type = "movie",
            Title = "Marvel One-Shot: Agent Carter",
            Year = 2013
        },
        new()
        {
            Index = 3,
            Type = "tv",
            Title = "Agent Carter",
            Year = 2015,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 4,
            Type = "tv",
            Title = "Agent Carter",
            Year = 2015,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 5,
            Type = "movie",
            Title = "Captain Marvel",
            Year = 2019
        },
        new()
        {
            Index = 6,
            Type = "movie",
            Title = "Iron Man",
            Year = 2008
        },
        new()
        {
            Index = 7,
            Type = "movie",
            Title = "Iron Man 2",
            Year = 2010
        },
        new()
        {
            Index = 8,
            Type = "movie",
            Title = "The Incredible Hulk",
            Year = 2008
        },
        new()
        {
            Index = 9,
            Type = "movie",
            Title = "The Consultant",
            Year = 2011
        },
        new()
        {
            Index = 10,
            Type = "movie",
            Title = "A Funny Thing Happened on the Way to Thor's Hammer",
            Year = 2011
        },
        new()
        {
            Index = 11,
            Type = "movie",
            Title = "Thor",
            Year = 2011
        },
        new()
        {
            Index = 12,
            Type = "movie",
            Title = "The Avengers",
            Year = 2012
        },
        new()
        {
            Index = 13,
            Type = "movie",
            Title = "Item 47",
            Year = 2012
        },
        new()
        {
            Index = 14,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [1],
            Episodes = [1, 2, 3, 4, 5, 6, 7]
        },
        new()
        {
            Index = 15,
            Type = "movie",
            Title = "Thor: The Dark World",
            Year = 2013
        },
        new()
        {
            Index = 16,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [1],
            Episodes = [8, 9, 10, 11, 12, 13, 14, 15, 16]
        },
        new()
        {
            Index = 17,
            Type = "movie",
            Title = "Iron Man 3",
            Year = 2013
        },
        new()
        {
            Index = 18,
            Type = "movie",
            Title = "All Hail the King",
            Year = 2014
        },
        new()
        {
            Index = 19,
            Type = "movie",
            Title = "Captain America: The Winter Soldier",
            Year = 2014
        },
        new()
        {
            Index = 20,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [1],
            Episodes = [17, 18, 19, 20, 21, 22]
        },
        new()
        {
            Index = 21,
            Type = "movie",
            Title = "Guardians of the Galaxy",
            Year = 2014
        },
        new()
        {
            Index = 22,
            Type = "movie",
            Title = "Guardians of the Galaxy Vol 2",
            Year = 2017
        },
        new()
        {
            Index = 23,
            Type = "tv",
            Title = "I Am Groot",
            Year = 2022,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 24,
            Type = "tv",
            Title = "I Am Groot",
            Year = 2022,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 25,
            Type = "tv",
            Title = "Daredevil",
            Year = 2015,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 26,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [2],
            Episodes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
        },
        new()
        {
            Index = 27,
            Type = "tv",
            Title = "Jessica Jones",
            Year = 2015,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 28,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [2],
            Episodes = [11, 12, 13, 14, 15, 16, 17, 18, 19]
        },
        new()
        {
            Index = 29,
            Type = "movie",
            Title = "Avengers: Age of Ultron",
            Year = 2015
        },
        new()
        {
            Index = 30,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [2],
            Episodes = [20, 21, 22]
        },
        new()
        {
            Index = 31,
            Type = "tv",
            Title = "Daredevil",
            Year = 2015,
            Seasons = [2],
            Episodes = [1, 2, 3, 4]
        },
        new()
        {
            Index = 32,
            Type = "tv",
            Title = "Luke Cage",
            Year = 2016,
            Seasons = [1],
            Episodes = [1, 2, 3, 4]
        },
        new()
        {
            Index = 33,
            Type = "tv",
            Title = "Daredevil",
            Year = 2015,
            Seasons = [2],
            Episodes = [5, 6, 7, 8, 9, 10, 11]
        },
        new()
        {
            Index = 34,
            Type = "tv",
            Title = "Luke Cage",
            Year = 2016,
            Seasons = [1],
            Episodes = [5, 6, 7, 8]
        },
        new()
        {
            Index = 35,
            Type = "tv",
            Title = "Daredevil",
            Year = 2015,
            Seasons = [2],
            Episodes = [12, 13]
        },
        new()
        {
            Index = 36,
            Type = "tv",
            Title = "Luke Cage",
            Year = 2016,
            Seasons = [1],
            Episodes = [9, 10, 11, 12, 13]
        },
        new()
        {
            Index = 37,
            Type = "movie",
            Title = "Ant-Man",
            Year = 2015
        },
        new()
        {
            Index = 38,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [3],
            Episodes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
        },
        new()
        {
            Index = 39,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [3],
            Episodes = [11, 12, 13, 14, 15, 16, 17, 18, 19]
        },
        new()
        {
            Index = 40,
            Type = "tv",
            Title = "Iron Fist",
            Year = 2017,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 41,
            Type = "movie",
            Title = "Captain America: Civil War",
            Year = 2016
        },
        new()
        {
            Index = 42,
            Type = "movie",
            Title = "Team Thor",
            Year = 2016
        },
        new()
        {
            Index = 43,
            Type = "movie",
            Title = "Team Thor: Part 2",
            Year = 2017
        },
        new()
        {
            Index = 44,
            Type = "movie",
            Title = "Black Widow",
            Year = 2021
        },
        new()
        {
            Index = 45,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [3],
            Episodes = [20, 21, 22]
        },
        new()
        {
            Index = 46,
            Type = "tv",
            Title = "The Defenders",
            Year = 2017,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 47,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [4],
            Episodes = [1, 2, 3, 4, 5, 6]
        },
        new()
        {
            Index = 48,
            Type = "movie",
            Title = "Doctor Strange",
            Year = 2016
        },
        new()
        {
            Index = 49,
            Type = "movie",
            Title = "Black Panther",
            Year = 2018
        },
        new()
        {
            Index = 50,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [4],
            Episodes = [7, 8]
        },
        new()
        {
            Index = 51,
            Type = "tv",
            Title = "Agents of SHIELD: Slingshot",
            Year = 2016,
            Seasons = [1],
            Episodes = [1, 2, 3, 4, 5, 6]
        },
        new()
        {
            Index = 52,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [4],
            Episodes = [9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22]
        },
        new()
        {
            Index = 53,
            Type = "movie",
            Title = "Spider-Man: Homecoming",
            Year = 2017
        },
        new()
        {
            Index = 54,
            Type = "movie",
            Title = "Thor: Ragnarok",
            Year = 2017
        },
        new()
        {
            Index = 55,
            Type = "movie",
            Title = "Team Darryl",
            Year = 2018
        },
        new()
        {
            Index = 56,
            Type = "tv",
            Title = "Inhumans",
            Year = 2017,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 57,
            Type = "tv",
            Title = "The Punisher",
            Year = 2017,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 58,
            Type = "tv",
            Title = "Runaways",
            Year = 2017,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 59,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [5],
            Episodes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
        },
        new()
        {
            Index = 60,
            Type = "tv",
            Title = "Jessica Jones",
            Year = 2015,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 61,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [5],
            Episodes = [11, 12, 13, 14, 15, 16, 17, 18]
        },
        new()
        {
            Index = 62,
            Type = "tv",
            Title = "Cloak & Dagger",
            Year = 2018,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 63,
            Type = "tv",
            Title = "Cloak & Dagger",
            Year = 2018,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 64,
            Type = "tv",
            Title = "Luke Cage",
            Year = 2016,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 65,
            Type = "tv",
            Title = "Iron Fist",
            Year = 2017,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 66,
            Type = "tv",
            Title = "Daredevil",
            Year = 2015,
            Seasons = [3],
            Episodes = []
        },
        new()
        {
            Index = 67,
            Type = "tv",
            Title = "Runaways",
            Year = 2017,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 68,
            Type = "tv",
            Title = "The Punisher",
            Year = 2017,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 69,
            Type = "tv",
            Title = "Jessica Jones",
            Year = 2015,
            Seasons = [3],
            Episodes = []
        },
        new()
        {
            Index = 70,
            Type = "movie",
            Title = "Ant-Man and the Wasp",
            Year = 2018
        },
        new()
        {
            Index = 71,
            Type = "movie",
            Title = "Avengers: Infinity War",
            Year = 2018
        },
        new()
        {
            Index = 72,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [5],
            Episodes = [19, 20, 21, 22]
        },
        new()
        {
            Index = 73,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [6],
            Episodes = []
        },
        new()
        {
            Index = 74,
            Type = "tv",
            Title = "Agents of SHIELD",
            Year = 2013,
            Seasons = [7],
            Episodes = []
        },
        new()
        {
            Index = 75,
            Type = "tv",
            Title = "Runaways",
            Year = 2017,
            Seasons = [3],
            Episodes = []
        },
        new()
        {
            Index = 76,
            Type = "movie",
            Title = "Avengers: Endgame",
            Year = 2019
        },
        new()
        {
            Index = 77,
            Type = "tv",
            Title = "Loki",
            Year = 2021,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 78,
            Type = "tv",
            Title = "Loki",
            Year = 2021,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 79,
            Type = "tv",
            Title = "What If...?",
            Year = 2021,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 80,
            Type = "tv",
            Title = "What If...?",
            Year = 2021,
            Seasons = [2],
            Episodes = []
        },
        new()
        {
            Index = 81,
            Type = "tv",
            Title = "WandaVision",
            Year = 2021,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 82,
            Type = "tv",
            Title = "The Falcon and the Winter Soldier",
            Year = 2021,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 83,
            Type = "movie",
            Title = "Shang-Chi and the Legend of the Ten Rings",
            Year = 2021
        },
        new()
        {
            Index = 84,
            Type = "movie",
            Title = "Eternals",
            Year = 2021
        },
        new()
        {
            Index = 85,
            Type = "movie",
            Title = "Spider-Man: Far From Home",
            Year = 2019
        },
        new()
        {
            Index = 86,
            Type = "movie",
            Title = "Spider-Man: No Way Home",
            Year = 2021
        },
        new()
        {
            Index = 87,
            Type = "movie",
            Title = "Doctor Strange in the Multiverse of Madness",
            Year = 2022
        },
        new()
        {
            Index = 88,
            Type = "tv",
            Title = "Hawkeye",
            Year = 2021,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 89,
            Type = "tv",
            Title = "Moon Knight",
            Year = 2022,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 90,
            Type = "movie",
            Title = "Black Panther: Wakanda Forever",
            Year = 2022
        },
        new()
        {
            Index = 91,
            Type = "tv",
            Title = "Echo",
            Year = 2024,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 92,
            Type = "tv",
            Title = "She-Hulk: Attorney at Law",
            Year = 2022,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 93,
            Type = "tv",
            Title = "Ms Marvel",
            Year = 2022,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 94,
            Type = "movie",
            Title = "Thor: Love and Thunder",
            Year = 2022
        },
        new()
        {
            Index = 95,
            Type = "movie",
            Title = "Werewolf by Night",
            Year = 2022
        },
        new()
        {
            Index = 96,
            Type = "movie",
            Title = "The Guardians of the Galaxy Holiday Special",
            Year = 2022
        },
        new()
        {
            Index = 97,
            Type = "movie",
            Title = "Ant-Man and The Wasp: Quantumania",
            Year = 2023
        },
        new()
        {
            Index = 98,
            Type = "movie",
            Title = "Guardians of the Galaxy Vol 3",
            Year = 2023
        },
        new()
        {
            Index = 99,
            Type = "tv",
            Title = "Secret Invasion",
            Year = 2023,
            Seasons = [1],
            Episodes = []
        },
        new()
        {
            Index = 100,
            Type = "movie",
            Title = "The Marvels",
            Year = 2023
        }
    ];
}