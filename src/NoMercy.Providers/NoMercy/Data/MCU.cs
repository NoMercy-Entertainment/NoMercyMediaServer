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
            index = 1,
            type = "movie",
            title = "Captain America: The First Avenger",
            year = 2011
        },
        new()
        {
            index = 2,
            type = "movie",
            title = "Marvel One-Shot: Agent Carter",
            year = 2013
        },
        new()
        {
            index = 3,
            type = "tv",
            title = "Agent Carter",
            year = 2015,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 4,
            type = "tv",
            title = "Agent Carter",
            year = 2015,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 5,
            type = "movie",
            title = "Captain Marvel",
            year = 2019
        },
        new()
        {
            index = 6,
            type = "movie",
            title = "Iron Man",
            year = 2008
        },
        new()
        {
            index = 7,
            type = "movie",
            title = "Iron Man 2",
            year = 2010
        },
        new()
        {
            index = 8,
            type = "movie",
            title = "The Incredible Hulk",
            year = 2008
        },
        new()
        {
            index = 9,
            type = "movie",
            title = "The Consultant",
            year = 2011
        },
        new()
        {
            index = 10,
            type = "movie",
            title = "A Funny Thing Happened on the Way to Thor's Hammer",
            year = 2011
        },
        new()
        {
            index = 11,
            type = "movie",
            title = "Thor",
            year = 2011
        },
        new()
        {
            index = 12,
            type = "movie",
            title = "The Avengers",
            year = 2012
        },
        new()
        {
            index = 13,
            type = "movie",
            title = "Item 47",
            year = 2012
        },
        new()
        {
            index = 14,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [1],
            episodes = [1, 2, 3, 4, 5, 6, 7]
        },
        new()
        {
            index = 15,
            type = "movie",
            title = "Thor: The Dark World",
            year = 2013
        },
        new()
        {
            index = 16,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [1],
            episodes = [8, 9, 10, 11, 12, 13, 14, 15, 16]
        },
        new()
        {
            index = 17,
            type = "movie",
            title = "Iron Man 3",
            year = 2013
        },
        new()
        {
            index = 18,
            type = "movie",
            title = "All Hail the King",
            year = 2014
        },
        new()
        {
            index = 19,
            type = "movie",
            title = "Captain America: The Winter Soldier",
            year = 2014
        },
        new()
        {
            index = 20,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [1],
            episodes = [17, 18, 19, 20, 21, 22]
        },
        new()
        {
            index = 21,
            type = "movie",
            title = "Guardians of the Galaxy",
            year = 2014
        },
        new()
        {
            index = 22,
            type = "movie",
            title = "Guardians of the Galaxy Vol 2",
            year = 2017
        },
        new()
        {
            index = 23,
            type = "tv",
            title = "I Am Groot",
            year = 2022,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 24,
            type = "tv",
            title = "I Am Groot",
            year = 2022,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 25,
            type = "tv",
            title = "Daredevil",
            year = 2015,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 26,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [2],
            episodes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
        },
        new()
        {
            index = 27,
            type = "tv",
            title = "Jessica Jones",
            year = 2015,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 28,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [2],
            episodes = [11, 12, 13, 14, 15, 16, 17, 18, 19]
        },
        new()
        {
            index = 29,
            type = "movie",
            title = "Avengers: Age of Ultron",
            year = 2015
        },
        new()
        {
            index = 30,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [2],
            episodes = [20, 21, 22]
        },
        new()
        {
            index = 31,
            type = "tv",
            title = "Daredevil",
            year = 2015,
            seasons = [2],
            episodes = [1, 2, 3, 4]
        },
        new()
        {
            index = 32,
            type = "tv",
            title = "Luke Cage",
            year = 2016,
            seasons = [1],
            episodes = [1, 2, 3, 4]
        },
        new()
        {
            index = 33,
            type = "tv",
            title = "Daredevil",
            year = 2015,
            seasons = [2],
            episodes = [5, 6, 7, 8, 9, 10, 11]
        },
        new()
        {
            index = 34,
            type = "tv",
            title = "Luke Cage",
            year = 2016,
            seasons = [1],
            episodes = [5, 6, 7, 8]
        },
        new()
        {
            index = 35,
            type = "tv",
            title = "Daredevil",
            year = 2015,
            seasons = [2],
            episodes = [12, 13]
        },
        new()
        {
            index = 36,
            type = "tv",
            title = "Luke Cage",
            year = 2016,
            seasons = [1],
            episodes = [9, 10, 11, 12, 13]
        },
        new()
        {
            index = 37,
            type = "movie",
            title = "Ant-Man",
            year = 2015
        },
        new()
        {
            index = 38,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [3],
            episodes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
        },
        new()
        {
            index = 39,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [3],
            episodes = [11, 12, 13, 14, 15, 16, 17, 18, 19]
        },
        new()
        {
            index = 40,
            type = "tv",
            title = "Iron Fist",
            year = 2017,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 41,
            type = "movie",
            title = "Captain America: Civil War",
            year = 2016
        },
        new()
        {
            index = 42,
            type = "movie",
            title = "Team Thor",
            year = 2016
        },
        new()
        {
            index = 43,
            type = "movie",
            title = "Team Thor: Part 2",
            year = 2017
        },
        new()
        {
            index = 44,
            type = "movie",
            title = "Black Widow",
            year = 2021
        },
        new()
        {
            index = 45,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [3],
            episodes = [20, 21, 22]
        },
        new()
        {
            index = 46,
            type = "tv",
            title = "The Defenders",
            year = 2017,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 47,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [4],
            episodes = [1, 2, 3, 4, 5, 6]
        },
        new()
        {
            index = 48,
            type = "movie",
            title = "Doctor Strange",
            year = 2016
        },
        new()
        {
            index = 49,
            type = "movie",
            title = "Black Panther",
            year = 2018
        },
        new()
        {
            index = 50,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [4],
            episodes = [7, 8]
        },
        new()
        {
            index = 51,
            type = "tv",
            title = "Agents of SHIELD: Slingshot",
            year = 2016,
            seasons = [1],
            episodes = [1, 2, 3, 4, 5, 6]
        },
        new()
        {
            index = 52,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [4],
            episodes = [9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22]
        },
        new()
        {
            index = 53,
            type = "movie",
            title = "Spider-Man: Homecoming",
            year = 2017
        },
        new()
        {
            index = 54,
            type = "movie",
            title = "Thor: Ragnarok",
            year = 2017
        },
        new()
        {
            index = 55,
            type = "movie",
            title = "Team Darryl",
            year = 2018
        },
        new()
        {
            index = 56,
            type = "tv",
            title = "Inhumans",
            year = 2017,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 57,
            type = "tv",
            title = "The Punisher",
            year = 2017,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 58,
            type = "tv",
            title = "Runaways",
            year = 2017,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 59,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [5],
            episodes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
        },
        new()
        {
            index = 60,
            type = "tv",
            title = "Jessica Jones",
            year = 2015,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 61,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [5],
            episodes = [11, 12, 13, 14, 15, 16, 17, 18]
        },
        new()
        {
            index = 62,
            type = "tv",
            title = "Cloak & Dagger",
            year = 2018,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 63,
            type = "tv",
            title = "Cloak & Dagger",
            year = 2018,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 64,
            type = "tv",
            title = "Luke Cage",
            year = 2016,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 65,
            type = "tv",
            title = "Iron Fist",
            year = 2017,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 66,
            type = "tv",
            title = "Daredevil",
            year = 2015,
            seasons = [3],
            episodes = []
        },
        new()
        {
            index = 67,
            type = "tv",
            title = "Runaways",
            year = 2017,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 68,
            type = "tv",
            title = "The Punisher",
            year = 2017,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 69,
            type = "tv",
            title = "Jessica Jones",
            year = 2015,
            seasons = [3],
            episodes = []
        },
        new()
        {
            index = 70,
            type = "movie",
            title = "Ant-Man and the Wasp",
            year = 2018
        },
        new()
        {
            index = 71,
            type = "movie",
            title = "Avengers: Infinity War",
            year = 2018
        },
        new()
        {
            index = 72,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [5],
            episodes = [19, 20, 21, 22]
        },
        new()
        {
            index = 73,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [6],
            episodes = []
        },
        new()
        {
            index = 74,
            type = "tv",
            title = "Agents of SHIELD",
            year = 2013,
            seasons = [7],
            episodes = []
        },
        new()
        {
            index = 75,
            type = "tv",
            title = "Runaways",
            year = 2017,
            seasons = [3],
            episodes = []
        },
        new()
        {
            index = 76,
            type = "movie",
            title = "Avengers: Endgame",
            year = 2019
        },
        new()
        {
            index = 77,
            type = "tv",
            title = "Loki",
            year = 2021,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 78,
            type = "tv",
            title = "Loki",
            year = 2021,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 79,
            type = "tv",
            title = "What If...?",
            year = 2021,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 80,
            type = "tv",
            title = "What If...?",
            year = 2021,
            seasons = [2],
            episodes = []
        },
        new()
        {
            index = 81,
            type = "tv",
            title = "WandaVision",
            year = 2021,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 82,
            type = "tv",
            title = "The Falcon and the Winter Soldier",
            year = 2021,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 83,
            type = "movie",
            title = "Shang-Chi and the Legend of the Ten Rings",
            year = 2021
        },
        new()
        {
            index = 84,
            type = "movie",
            title = "Eternals",
            year = 2021
        },
        new()
        {
            index = 85,
            type = "movie",
            title = "Spider-Man: Far From Home",
            year = 2019
        },
        new()
        {
            index = 86,
            type = "movie",
            title = "Spider-Man: No Way Home",
            year = 2021
        },
        new()
        {
            index = 87,
            type = "movie",
            title = "Doctor Strange in the Multiverse of Madness",
            year = 2022
        },
        new()
        {
            index = 88,
            type = "tv",
            title = "Hawkeye",
            year = 2021,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 89,
            type = "tv",
            title = "Moon Knight",
            year = 2022,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 90,
            type = "movie",
            title = "Black Panther: Wakanda Forever",
            year = 2022
        },
        new()
        {
            index = 91,
            type = "tv",
            title = "Echo",
            year = 2024,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 92,
            type = "tv",
            title = "She-Hulk: Attorney at Law",
            year = 2022,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 93,
            type = "tv",
            title = "Ms Marvel",
            year = 2022,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 94,
            type = "movie",
            title = "Thor: Love and Thunder",
            year = 2022
        },
        new()
        {
            index = 95,
            type = "movie",
            title = "Werewolf by Night",
            year = 2022
        },
        new()
        {
            index = 96,
            type = "movie",
            title = "The Guardians of the Galaxy Holiday Special",
            year = 2022
        },
        new()
        {
            index = 97,
            type = "movie",
            title = "Ant-Man and The Wasp: Quantumania",
            year = 2023
        },
        new()
        {
            index = 98,
            type = "movie",
            title = "Guardians of the Galaxy Vol 3",
            year = 2023
        },
        new()
        {
            index = 99,
            type = "tv",
            title = "Secret Invasion",
            year = 2023,
            seasons = [1],
            episodes = []
        },
        new()
        {
            index = 100,
            type = "movie",
            title = "The Marvels",
            year = 2023
        }
    ];
}