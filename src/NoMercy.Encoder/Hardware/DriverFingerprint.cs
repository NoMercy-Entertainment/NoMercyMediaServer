namespace NoMercy.Encoder.Hardware;

using System.Security.Cryptography;
using System.Text;

public record GpuDriverInfo(string Vendor, string Model, string DriverVersion, int Index);

public record DriverFingerprint(IReadOnlyList<GpuDriverInfo> Gpus)
{
    /// <summary>
    /// Computes a stable lowercase hex SHA-256 over the GPU list.
    /// Entries are sorted ordinally before hashing so insertion-order changes
    /// do not produce a false positive (e.g. driver enumerating GPUs in a
    /// different order after a reboot).
    /// </summary>
    public string ComputeHash()
    {
        IEnumerable<string> parts = Gpus.Select(g => $"{g.Vendor}|{g.Model}|{g.DriverVersion}")
            .OrderBy(s => s, StringComparer.Ordinal);

        string payload = string.Join(";", parts);
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }
}
