using QuestPDF.Infrastructure;

namespace CasaAndina.Infrastructure.Services;

internal static class PdfBranding
{
    public static byte[] Logo => Assets.Value;
    private static readonly Lazy<byte[]> Assets = new(() =>
    {
        // Proyecto académico / uso individual: licencia Community de QuestPDF.
        QuestPDF.Settings.License = LicenseType.Community;
        var assembly = typeof(PdfBranding).Assembly;
        using var font = assembly.GetManifestResourceStream("CasaAndina.Infrastructure.Assets.HankenGrotesk.ttf")
            ?? throw new InvalidOperationException("No se encontró la tipografía del PDF.");
        QuestPDF.Drawing.FontManager.RegisterFontFromStream(font);
        using var logo = assembly.GetManifestResourceStream("CasaAndina.Infrastructure.Assets.casa-andina.png")
            ?? throw new InvalidOperationException("No se encontró el logo del PDF.");
        using var memory = new MemoryStream();
        logo.CopyTo(memory);
        return memory.ToArray();
    });
}
