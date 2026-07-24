using Axon.Infrastructure.Services;
using ZXing;

namespace Axon.Application.Tests.Inventory;

public class BarcodeServiceTests
{
    // Generación de código de barras para un producto nuevo, formato válido:
    // se valida el encabezado BMP (sin System.Drawing/SkiaSharp, ver comentario
    // en BarcodeService) Y, más importante, que el contenido decodifica de
    // vuelta al SKU original con un lector Code128 real (ZXing) — no solo que
    // "parece una imagen", sino que es un código de barras legible de verdad.
    [Fact]
    public void GenerateBarcode_ForProductSku_ProducesValidDecodableCode128Bmp()
    {
        var service = new BarcodeService();
        const string sku = "MART-001";

        var bmp = service.GenerateBarcode(sku);

        // Encabezado BMP válido ("BM" + dimensiones positivas).
        Assert.Equal((byte)'B', bmp[0]);
        Assert.Equal((byte)'M', bmp[1]);

        var width = BitConverter.ToInt32(bmp, 18);
        var height = BitConverter.ToInt32(bmp, 22);
        Assert.True(width > 0);
        Assert.True(height > 0);

        var pixelDataOffset = BitConverter.ToInt32(bmp, 10);
        var pixelData = bmp[pixelDataOffset..];

        // El orden de filas (BMP es bottom-up) no afecta la decodificación de un
        // código de barras 1D: todas las filas repiten el mismo patrón de barras.
        var luminanceSource = new RGBLuminanceSource(pixelData, width, height, RGBLuminanceSource.BitmapFormat.RGB24);
        var binaryBitmap = new ZXing.BinaryBitmap(new ZXing.Common.HybridBinarizer(luminanceSource));

        var hints = new Dictionary<ZXing.DecodeHintType, object>
        {
            [ZXing.DecodeHintType.POSSIBLE_FORMATS] = new List<BarcodeFormat> { BarcodeFormat.CODE_128 },
            [ZXing.DecodeHintType.TRY_HARDER] = true
        };

        var result = new MultiFormatReader().decode(binaryBitmap, hints);

        Assert.NotNull(result);
        Assert.Equal(sku, result!.Text);
        Assert.Equal(BarcodeFormat.CODE_128, result.BarcodeFormat);
    }

    [Fact]
    public void GenerateBarcode_ForDifferentSkus_ProducesDifferentContent()
    {
        var service = new BarcodeService();

        var barcode1 = service.GenerateBarcode("SKU-AAA");
        var barcode2 = service.GenerateBarcode("SKU-BBB");

        Assert.NotEqual(barcode1, barcode2);
    }
}
