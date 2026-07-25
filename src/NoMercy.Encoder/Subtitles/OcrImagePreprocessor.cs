// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Pre-processing helpers that improve Tesseract OCR accuracy on bitmap
/// subtitle frames (PGS / VobSub / DVB).
///
/// <para>
/// <b>Limitation</b>: the current <see cref="ISubtitleOcrEngine"/> pipeline
/// drives FFmpeg's <c>ocr</c> filter and does not expose per-frame RGBA
/// pixel data. <see cref="PreprocessForOcr"/> therefore cannot be wired into
/// the live engine today. It is implemented here so future per-frame OCR work
/// (e.g. a libass-based frame extractor) can call it without re-deriving the
/// algorithm.
/// </para>
/// </summary>
public static class OcrImagePreprocessor
{
    /// <summary>
    /// Composites an RGBA subtitle frame onto a neutral grey (RGB 128)
    /// background using luminance-weighted alpha blending:
    /// <code>
    ///   out = alpha * luminance(rgb) + (1 - alpha) * 128
    /// </code>
    /// This preserves anti-aliased edge contrast on a mid-grey ground rather
    /// than the default black-on-transparent, which causes thin letter strokes
    /// to feather into the background and confuse Tesseract.
    ///
    /// <para>Input must be tightly-packed RGBA (4 bytes per pixel, row-major).</para>
    /// <para>Returns a new greyscale byte array (1 byte per pixel, same WxH).</para>
    /// </summary>
    /// <param name="rgbaImage">Raw RGBA pixel data, length must equal width * height * 4.</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <returns>
    /// Greyscale image (1 byte per pixel) composited onto neutral grey.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="rgbaImage"/> length does not match
    /// <c>width * height * 4</c>.
    /// </exception>
    public static byte[] PreprocessForOcr(byte[] rgbaImage, int width, int height)
    {
        int expectedLength = width * height * 4;
        if (rgbaImage.Length != expectedLength)
            throw new ArgumentException(
                $"Expected RGBA buffer of {expectedLength} bytes for {width}x{height} frame, "
                         + $"got {rgbaImage.Length}.",
                nameof(rgbaImage)
            );

        byte[] grey = new byte[width * height];
        int pixelCount = width * height;

        for (int i = 0; i < pixelCount; i++)
        {
            int base4 = i * 4;
            double r = rgbaImage[base4];
            double g = rgbaImage[base4 + 1];
            double b = rgbaImage[base4 + 2];
            double a = rgbaImage[base4 + 3] / 255.0;

            // BT.601 luma: weights match human perception of red/green/blue.
            double luma = 0.299 * r + 0.587 * g + 0.114 * b;

            double composited = a * luma + (1.0 - a) * 128.0;

            grey[i] = (byte)Math.Clamp(Math.Round(composited), 0.0, 255.0);
        }

        return grey;
    }
}
