using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Queue.system;

namespace NoMercy.Server.app.Jobs;

public class AddMoviesJob : IShouldQueue
{
    public AddMoviesJob()
    {
    }
    
    public async Task Handle()
    {
        int[] movieIds =
        [
            13654, 584, 38142, 17979, 15789, 937278, 40073, 54272, 21174, 20737, 21661, 21518, 24794, 20755, 149,
            1074034, 15653, 13448, 102899, 363088, 640146, 1579, 11633, 45295, 269650, 271924, 265805, 13981, 99861,
            299534,
            299536, 46842, 339403, 628861, 40260, 9737, 8961, 573435, 38700, 15805, 408648, 14919, 21683,
            1016121, 1096342, 10020, 13313, 20235, 34075, 702525, 497467, 284054, 505642, 497698, 78, 335984, 15049,
            417489, 62177, 11932, 400106, 417644, 406956, 253774, 253777, 822119, 271110, 1771, 100402, 299537, 920,
            49013, 260514, 46844, 12225, 10515, 39860, 53014, 640, 302699, 818119, 198184, 181283, 10585, 11186,
            11187, 354912, 358332, 1948, 15092, 1640, 620705, 312221, 480530, 677179, 302156, 64325, 412452, 641228,
            53849, 53854, 393345, 167032, 320288, 293660, 383498, 533535, 351460, 20352, 93456, 324852, 519182,
            127517, 68718, 284052, 453395, 39920, 55508, 39921, 64690, 329996, 493529, 35435, 44734, 9761, 568124,
            181886, 14821, 15137, 22843, 75629, 283566, 857862, 545609, 697843, 1141700, 385128, 756, 49948, 259316,
            338952, 338953, 13804, 82992, 384018, 51497, 385687, 755679, 54266, 522402, 40349, 56710, 513347, 40470,
            6145, 40472, 16084, 83389, 109445, 1024604, 1205983, 330457, 168259, 3131, 400928, 205584, 801, 769,
            12477, 118340, 283995, 447365, 457335, 899082, 672, 12444, 12445, 674, 767, 675, 671, 673, 67823, 67824,
            99089, 40533, 1620, 522931, 249070, 51540, 227159, 10191, 82702, 638507, 166428, 4935, 1927, 425,
            278154, 57800, 8355, 950, 260513, 335977, 217, 89, 87, 207932, 667216, 157336, 1726, 10138, 68721,
            346364, 474350, 68620, 68622, 68625, 68630, 68633, 68986, 245891, 324552, 458156, 603692, 730629,
            475557, 889737, 617502, 8844, 512200, 353486, 329, 331, 135397, 507086, 351286, 69096, 120811, 449503,
            1215162, 40362, 1995, 1996, 941, 942, 943, 944, 583, 72000, 718789, 72669, 72675, 72679, 193756, 253835,
            73139, 585511, 1162244, 614587, 1162239, 1027159, 80863, 78139, 39954, 80405, 31051, 31050, 51943,
            109572, 31049, 80857, 572616, 154189, 26546, 567767, 31599, 404532, 491667, 31053, 80865, 80868, 172591,
            245928, 641909, 251432, 33176, 80860, 80866, 59719, 15371, 31594, 50482, 80861, 39952, 32873, 30143,
            45378, 53894, 31595, 76190, 101, 438435, 102651, 420809, 76535, 211387, 253980, 119569, 76122, 592687,
            576743, 583209, 592688, 592689, 491633, 433, 400650, 464566, 82, 73627, 211672, 438148, 954, 575264,
            353081, 56292, 177677, 575265, 955, 956, 40024, 40144, 73690, 407436, 337401, 11452, 2059, 6637, 983058,
            74176, 21832, 18491, 18510, 58995, 74530, 317091, 21057, 402900, 161, 298, 163, 40369, 12233, 311,
            12230, 1018494, 872585, 411999, 40370, 676, 75121, 11114, 285, 58, 166426, 1865, 22, 10530, 13761,
            10991, 12600, 447404, 33875, 16808, 12599, 115223, 303903, 227679, 350499, 436931, 150213, 571891,
            662708, 494407, 382190, 88557, 39057, 34065, 47292, 36218, 34067, 10228, 25961, 50087, 75658, 31102,
            15283, 12429, 11621, 75975, 106, 169, 34851, 766507, 76006, 296917, 452015, 24701, 76009, 76015, 40372,
            40163, 39842, 40164, 13836, 85, 404368, 44896, 527774, 1576, 35791, 1577, 133121, 1083862, 13648, 7737,
            71679, 173897, 400136, 14822, 1892, 54270, 330459, 77566, 40033, 54281, 77609, 4232, 646385, 4233, 4234,
            41446, 1159559, 934433, 11249, 355131, 1036561, 484886, 45745, 79253, 79256, 46862, 39887, 454626,
            675353, 939243, 1219926, 974691, 993729, 1190012, 1012290, 557, 558, 559, 969681, 569094, 911916,
            429617, 315635, 324857, 634649, 129, 40209, 11, 1893, 1894, 721334, 1895, 140607, 181808, 181812, 40212,
            413594, 533533, 20526, 37933, 38757, 413279, 133701, 40216, 421611, 280, 296, 87101, 534, 290859, 90912,
            92010, 1930, 102382, 153518, 454640, 24428, 629542, 414906, 806704, 10957, 890771, 434920, 39888, 40046,
            39853, 15370, 53002, 339988, 11873, 18357, 591, 94419, 426814, 55533, 1891, 444193, 9799, 9615, 337339,
            69116, 10948, 9948, 57233, 198375, 336000, 105864, 340270, 390043, 49051, 122917, 57158, 93014, 456048,
            302150, 990, 774825, 1724, 9806, 40232, 454983, 583083, 727745, 339404, 8698, 8587, 11430, 9732, 10144,
            10898, 13676, 72668, 206171, 39892, 73723, 120, 122, 121, 330, 647250, 708702, 609681, 603, 604, 624860,
            605, 40234, 40450, 73632, 73676, 552688, 40235, 282035, 39894, 227783, 335777, 12924, 758323, 346910,
            11319, 11135, 433808, 51739, 843241, 989937, 1064835, 507569, 372343, 413644, 149871, 16692, 16693,
            456538, 218, 40239, 916192, 46852, 416160, 405775, 149870, 39896, 40457, 401898, 10195, 616037, 284053,
            76338, 42779, 605886, 587807, 338970, 862, 863, 10193, 301528, 1084244, 456210, 1858, 91314, 25565,
            38356, 8373, 335988, 40250, 94177, 97, 97917, 3536, 335787, 1216425, 14160, 10032, 19824, 397415,
            335983, 912649, 580489, 533514, 700935, 10681, 13183, 246741, 471014, 242828, 37797, 82690, 36657,
            246655, 127585, 49538, 36668, 36658, 372058, 54279, 54283, 42601, 24444, 54275, 40897, 54274, 54280,
            54284, 54306, 18624, 18511, 54278, 54277, 54307, 54265, 54276, 54273, 21410, 40644, 269149, 1084242,
        ];
        
        await using MediaContext context = new MediaContext();
        Library? library = await context.Libraries
            .Include(l => l.FolderLibraries)
            .ThenInclude(fl => fl.Folder)
            .Where(f => f.Title == "Films")
            .FirstOrDefaultAsync();
        
        if (library is null) return;

        foreach (var id in movieIds)
        {
            AddMovieJob addMovieJob = new AddMovieJob(id:id, libraryId:library.Id.ToString());
            JobDispatcher.Dispatch(addMovieJob, "queue", 5);
        }
    }
}