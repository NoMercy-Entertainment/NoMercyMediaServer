using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbCertification
{
    [JsonProperty("certifications")] public TmdbCertificationList TmdbCertifications { get; set; } = new();

    public List<TmdbCertificationItem> ToArray()
    {
        List<TmdbCertificationItem> certifications = new();

        int index = 0;
        while (index < TmdbCertifications.Au.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Au[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "AU"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Bg.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Bg[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "BG"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Br.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Br[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "BR"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Caqc.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Caqc[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "CAQC"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Ca.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Ca[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "CA"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.De.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.De[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "DE"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Dk.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Dk[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "DK"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Es.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Es[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "ES"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Fi.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Fi[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "FI"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Fr.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Fr[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "FR"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Gb.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Gb[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "GB"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Hu.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Hu[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "HU"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.In.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.In[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "IN"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.It.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.It[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "IT"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Kr.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Kr[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "KR"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Lt.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Lt[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "LT"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.My.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.My[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "MY"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Nl.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Nl[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "NL"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.No.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.No[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "NO"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Nz.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Nz[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "NZ"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Ph.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Ph[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "PH"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Pt.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Pt[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "PT"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Ru.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Ru[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "RU"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Se.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Se[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "SE"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Sk.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Sk[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "SK"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Th.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Th[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "TH"
            });
            index += 1;
        }

        index = 0;
        while (index < TmdbCertifications.Us.Length)
        {
            TmdbCertificationItem y = TmdbCertifications.Us[index];
            certifications.Add(new()
            {
                Certification = y.Certification,
                Meaning = y.Meaning,
                Order = y.Order,
                Iso31661 = "US"
            });
            index += 1;
        }

        return certifications;
    }
}