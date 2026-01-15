using QRCoder;

namespace NoMercy.Setup;

public class ConsoleQrCode
{
    public static void Display(string text)
    {
        QRCodeGenerator generator = new();
        QRCodeData data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.L);
        string qrCode = new AsciiQRCode(data).GetGraphic(1);

        // Ensure we're writing a string, not char array
        foreach (string line in qrCode.Split('\n'))
            // Logger.Auth(line);
            Console.WriteLine(line);
    }
}