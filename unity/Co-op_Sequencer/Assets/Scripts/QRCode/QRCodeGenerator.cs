using UnityEngine;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

/// <summary>
/// Wraps ZXing.Net to produce a QR code Texture2D.
///
/// SETUP (one-time):
///   1. Download zxing.unity.dll from https://github.com/micjahn/ZXing.Net/releases
///      (pick the latest release, grab zxing.unity.dll from the zip)
///   2. Drop it into:  Assets/Plugins/zxing.unity.dll
///   3. Unity will recompile and this script will work.
///
/// Usage:
///   Texture2D tex = QRCodeGenerator.Generate("hello", pixelsPerModule: 10);
/// </summary>
public static class QRCodeGenerator
{
    /// <summary>
    /// Encodes <paramref name="text"/> as a QR code and returns a Texture2D.
    /// </summary>
    /// <param name="text">String to encode.</param>
    /// <param name="pixelsPerModule">Size of each module in pixels (higher = larger image).</param>
    /// <param name="darkColor">Module colour (default black).</param>
    /// <param name="lightColor">Background colour (default white).</param>
    public static Texture2D Generate(
        string text,
        int    pixelsPerModule = 10,
        Color? darkColor       = null,
        Color? lightColor      = null)
    {
        var dark  = darkColor  ?? Color.black;
        var light = lightColor ?? Color.white;

        // Step 1: encode to a BitMatrix at 1px-per-module (no scaling artefacts).
        var hints = new System.Collections.Generic.Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.ERROR_CORRECTION] = ZXing.QrCode.Internal.ErrorCorrectionLevel.M,
            [EncodeHintType.CHARACTER_SET]    = "UTF-8",
            [EncodeHintType.MARGIN]           = 0,
        };

        var encoder = new QRCodeWriter();
        ZXing.Common.BitMatrix matrix = encoder.encode(text, BarcodeFormat.QR_CODE, 0, 0, hints);

        int qrSize = matrix.Width;
        int quiet  = 4;
        int total  = (qrSize + quiet * 2) * pixelsPerModule;

        // Step 2: paint modules manually — guarantees pixel-perfect, no interpolation.
        var tex = new Texture2D(total, total, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[total * total];

        Color32 darkC  = new Color32(
            (byte)(dark.r  * 255), (byte)(dark.g  * 255), (byte)(dark.b  * 255), 255);
        Color32 lightC = new Color32(
            (byte)(light.r * 255), (byte)(light.g * 255), (byte)(light.b * 255), 255);

        // Fill white first
        for (int i = 0; i < pixels.Length; i++) pixels[i] = lightC;

        int offset = quiet * pixelsPerModule;

        for (int row = 0; row < qrSize; row++)
        {
            for (int col = 0; col < qrSize; col++)
            {
                if (!matrix[col, row]) continue; // light — already filled

                // Unity tex: y=0 is bottom, QR row=0 is top → flip
                int px = offset + col * pixelsPerModule;
                int py = offset + (qrSize - 1 - row) * pixelsPerModule;

                for (int dy = 0; dy < pixelsPerModule; dy++)
                    for (int dx = 0; dx < pixelsPerModule; dx++)
                        pixels[(py + dy) * total + (px + dx)] = darkC;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
