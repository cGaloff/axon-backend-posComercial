using Axon.Domain.Interfaces;
using ZXing;
using ZXing.Common;
using ZXing.OneD;

namespace Axon.Infrastructure.Services;

// Code128 (no EAN13): EAN13 exige un contenido puramente numérico de 12-13
// dígitos, incompatible con el formato de SKU libre que ya usa este proyecto
// (ej. "MART-001", visto en scripts/test-*.ps1). Code128 codifica cualquier
// texto alfanumérico sin restricción de formato, así que el SKU existente se
// usa tal cual, sin inventar un nuevo esquema de códigos numéricos.
public class BarcodeService : IBarcodeService
{
    private const int RequestedWidth = 300;
    private const int RequestedHeight = 80;

    public byte[] GenerateBarcode(string content)
    {
        var writer = new Code128Writer();
        var matrix = writer.encode(content, BarcodeFormat.CODE_128, RequestedWidth, RequestedHeight);

        return ToBmp(matrix);
    }

    // BMP monocromo codificado a mano, sin System.Drawing ni SkiaSharp — mismo
    // criterio que el QR existente (Net.Codecrete.QrCodeGenerator.ToBmpBitmap):
    // cero dependencias nativas, funciona igual en Windows/Linux/contenedores
    // sin necesitar librerías gráficas del sistema operativo.
    private static byte[] ToBmp(BitMatrix matrix)
    {
        var width = matrix.Width;
        var height = matrix.Height;

        var rowSize = (width * 3 + 3) / 4 * 4; // cada fila alineada a 4 bytes
        var pixelDataSize = rowSize * height;
        var fileSize = 54 + pixelDataSize;

        var bytes = new byte[fileSize];

        // Encabezado BMP (14 bytes).
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        WriteInt32(bytes, 2, fileSize);
        WriteInt32(bytes, 10, 54); // offset a los datos de píxeles

        // BITMAPINFOHEADER (40 bytes).
        WriteInt32(bytes, 14, 40);
        WriteInt32(bytes, 18, width);
        WriteInt32(bytes, 22, height); // positivo: bottom-up
        WriteInt16(bytes, 26, 1); // planes
        WriteInt16(bytes, 28, 24); // bits por pixel
        WriteInt32(bytes, 34, pixelDataSize);

        var offset = 54;

        for (var y = height - 1; y >= 0; y--) // BMP se escribe de abajo hacia arriba
        {
            for (var x = 0; x < width; x++)
            {
                var value = matrix[x, y] ? (byte)0 : (byte)255;
                bytes[offset++] = value; // B
                bytes[offset++] = value; // G
                bytes[offset++] = value; // R
            }

            offset += rowSize - (width * 3); // padding de la fila
        }

        return bytes;
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteInt16(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }
}
