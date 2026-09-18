using QRCoder;

namespace QuestBoard.Service.Helpers;

// Renders a scannable code as inline vector markup. The vector renderer (SvgQRCode) is used
// specifically -- never the bitmap renderer (PngByteQRCode) or the styled renderer (ArtQRCode) --
// because those need an image-encoder layer and, for the styled renderer, native drawing support
// that this container's image has neither of. SvgQRCode needs no image encoder and no native
// library, so it is the one renderer that can run here at all, not merely the nicest-looking one.
public static class CalendarSubscriptionQrCode
{
    // Returns null rather than throwing when generation fails. A caller renders every other way
    // of getting the address regardless -- the plain address text, the Copy button, the webcal
    // link -- and simply omits the code region when this returns null. A missing code costs the
    // reader one convenience; a thrown exception here would cost them the whole page render. This
    // does not hide the failure from an operator: the caller is expected to log it, naming only
    // the subscription's id and never the address, when this returns null.
    public static string? ToSvg(string payload, int pixelsPerModule = 4)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var svgQrCode = new SvgQRCode(data);
            return svgQrCode.GetGraphic(pixelsPerModule);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
