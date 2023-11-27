// See https://aka.ms/new-console-template for more information

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Docnet.Core;
using Docnet.Core.Editors;
using Docnet.Core.Models;

var watermark = File.ReadAllBytes("Resources/watermark.png");
var pdfFile = File.ReadAllBytes("Resources/pdfFile.pdf");

using (var library = DocLib.Instance)
{
    using (var watermarkMStream = new MemoryStream(watermark))
    {
        using var watermarkBitmap = ResizeImage(new Bitmap(watermarkMStream), 400, 200);

        using (var docReader = library.GetDocReader(pdfFile, new PageDimensions(1080, 1920)))
        {
            var pageCount = docReader.GetPageCount();

            var jpegFiles = new List<JpegImage>();
            
            for (var i = 0; i < pageCount; i++)
            {
                using var pageReader = docReader.GetPageReader(i);
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();
                var rawPageImage = pageReader.GetImage();

                using var outputBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                AddBytes(bmp, rawPageImage);

                //https://github.com/GowenGit/docnet/issues/8
                var g = Graphics.FromImage(outputBitmap);
                g.FillRegion(Brushes.White, new Region(new Rectangle(0, 0, width, height)));
                g.DrawImage(bmp, new Point(0, 0));
                g.DrawImage(watermarkBitmap, new Point(width - watermarkBitmap.Width, height - watermarkBitmap.Height));
                g.Save();

                using (var outputStream = new MemoryStream())
                {
                    outputBitmap.Save(outputStream, ImageFormat.Jpeg);
                    outputStream.Position = 0;
                    outputStream.Flush();
                        
                    jpegFiles.Add(new JpegImage
                    {
                        Bytes = outputStream.GetBuffer(),
                        Height = height,
                        Width = width,
                    });
                }
            }
            
            var bytes = library.JpegToPdf(jpegFiles);
            
            File.WriteAllBytes("output_file.pdf", bytes);
        }
    }
}

return;

void AddBytes(Bitmap bmp, byte[] rawBytes)
{
    var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);

    //Use the LockBits method to lock an existing bitmap in system memory
    //so that it can be changed programmatically.
    //see here https://docs.microsoft.com/ru-ru/dotnet/api/system.drawing.bitmap.lockbits?view=netcore-2.1
    var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
    // Gets the address of the first pixel data in the bitmap
    var pNative = bmpData.Scan0;

    Marshal.Copy(rawBytes, 0, pNative, rawBytes.Length);
    bmp.UnlockBits(bmpData);
}

// https://stackoverflow.com/questions/1922040/how-to-resize-an-image-c-sharp
static Bitmap ResizeImage(Image image, int width, int height)
{
    var destRect = new Rectangle(0, 0, width, height);
    var destImage = new Bitmap(width, height);

    destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

    using (var graphics = Graphics.FromImage(destImage))
    {
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using (var wrapMode = new ImageAttributes())
        {
            wrapMode.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(image, destRect, 0, 0, image.Width,image.Height, GraphicsUnit.Pixel, wrapMode);
        }
    }

    return destImage;
}