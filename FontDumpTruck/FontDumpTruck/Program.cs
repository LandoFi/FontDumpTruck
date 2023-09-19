using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Drawing.Text;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.IO.Ports;

namespace FontDumpTruck
{
    class ExportDefinition
    {
        public float EmValue { get; set; }
        public int ImageSize { get; set; }  
        public bool WriteOutput { get; set; }
    }
    class Program
    {
        static HashSet<string> EmptyCharacterShaHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "2f070d7b8728981a31c17be423fd02119041809abf58bd2b98e2d96a4a71c874", //16x16 missing
            "5016a323018f09e292165ad5392d82dcbad5e79c2b6b93aff3322dffff80b309", //16x16 empty
            "e0341dd152d792367abb61fe75c4fb7eec227e2ab3c3ef6388dc91bcae9aa25f", //16x16 missing zoomed
            "b3841228c51eaa713a6816abde9f9421079675186c76f82f38c2e57ba13c1ba7", //16x16 missing narrow
            "98dc1b13e3dac2bc63a5dccb76641eacfeb87e439114b2fc0bf83f6f0b6e5a49", //16x16 missing bold
            "fa4e1a6326059d4b6b28a770e543bc40348255ba03d068c35a83f2940048eec7", //16x16 via MDL2
            "4cb3cb49e25294803a68734b331fd161c6824bfc8f907b8f458ab958628383ec", //16x16 via em point resize
        };

        static List<ExportDefinition> EmPointsImageSizeMap = new List<ExportDefinition>
        {
            new ExportDefinition { EmValue = 15.0f, ImageSize = 16, WriteOutput = false },
            new ExportDefinition { EmValue = 30.0f, ImageSize = 32, WriteOutput = false },
            new ExportDefinition { EmValue = 60.0f, ImageSize = 64, WriteOutput = false },
            new ExportDefinition { EmValue = 120.0f, ImageSize = 128, WriteOutput = true },
            new ExportDefinition { EmValue = 240.0f, ImageSize = 256, WriteOutput = true },
            new ExportDefinition { EmValue = 480.0f, ImageSize = 512, WriteOutput = true },
            new ExportDefinition { EmValue = 960.0f, ImageSize = 1024, WriteOutput = false },
            new ExportDefinition { EmValue = 1920.0f, ImageSize = 2048, WriteOutput = false },
            new ExportDefinition { EmValue = 3840.0f, ImageSize = 4096, WriteOutput = false },
        };

        static List<ExportDefinition> ActiveImageSizes = new List<ExportDefinition>();

        static HashSet<int> SkippedCharacterIndexes = new HashSet<int>();
        static HashSet<int> InvalidCharacterIndexes = new HashSet<int>();

        static List<string> FontsToDump = new List<string>
        {
            "Cambria",
            "Wingdings",
            "Symbol"
        };

        static Dictionary<int, string> EnumeratedFontList = new Dictionary<int, string>();

        static ImageFormat OutputImageFormat = ImageFormat.Png;
        static readonly string OutputImageFileSuffix = OutputImageFormat.ToString().ToLower();

        static int asciiCharStart = 0;
        static int asciiCharStop = 65535;

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var flag = args[0];

                if (flag.Equals("-i", StringComparison.OrdinalIgnoreCase))
                {
                    PopulateEnumeratedFontList();

                    DisplayInstalledFonts();

                    Console.Write($"Enter number of font to dump: ");

                    var selectedFontInput = Console.ReadLine().Trim();

                    if (!int.TryParse(selectedFontInput, out int parsedInput))
                    {
                        Console.WriteLine($"Invalid input.");
                        return;
                    }

                    if (!EnumeratedFontList.ContainsKey(parsedInput))
                    {
                        Console.WriteLine($"Invalid font number {parsedInput}");
                        return;
                    }
                  
                    FontsToDump = new List<string> { EnumeratedFontList[parsedInput] };
                }
            }

            if (!EmPointsImageSizeMap.Where(e => e.WriteOutput).Any())
            {
                Console.WriteLine($"No image sizes to write out selected");
                return;
            }

            ActiveImageSizes.Add(EmPointsImageSizeMap.Where(e => e.ImageSize == 16).SingleOrDefault());
            ActiveImageSizes.AddRange(EmPointsImageSizeMap.Skip(1).Where(e => e.WriteOutput));

            var writtenImages = 0;
            var processedImages = 0;

            var totalSizeBatches = ActiveImageSizes.Count;
            var currentBatch = 1;

            Console.WriteLine($"Producing sizes: {string.Join(", ", ActiveImageSizes.Select(i => i.ImageSize.ToString()))}");

            foreach (var fontName in FontsToDump)
            {
                var imgOutputPath = $"{fontName}";

                if (!Directory.Exists(imgOutputPath))
                {
                    Directory.CreateDirectory(imgOutputPath);
                }

                foreach (var emPointAndImageSize in ActiveImageSizes)
                {
                    Console.WriteLine($"Starting batch: {emPointAndImageSize.ImageSize}. Write enabled: {emPointAndImageSize.WriteOutput}");

                    //this was a quick and super dirty test app, probably a better way to do this by enumerating all
                    //chars present in a .ttf
                    for (int i = asciiCharStart; i <= asciiCharStop; i++)
                    {
                        var fontEmPoints = emPointAndImageSize.EmValue;
                        var imageSize = emPointAndImageSize.ImageSize;
                        var writeEnabled = emPointAndImageSize.WriteOutput;

                        processedImages++;

                        if (i % 500 == 0)
                        {
                            Console.WriteLine($"(Batch {currentBatch}/{totalSizeBatches}) Processed 500 images ({processedImages} total images). Written {writtenImages} images; Skipped {SkippedCharacterIndexes.Count} empty characters");
                        }

                        if (SkippedCharacterIndexes.Contains(i) || InvalidCharacterIndexes.Contains(i))
                        {
                            continue;
                        }

                        var currentCharacter = (char)i;

                        var imgOutputFileName = fontName + "\\" + $"{fontName}_char-{i}_{imageSize}x{imageSize}.{OutputImageFileSuffix}";

                        if (File.Exists(imgOutputFileName))
                        {
                            writtenImages++;
                            continue;
                        }

                        Font font = new Font(fontName, fontEmPoints, FontStyle.Regular, GraphicsUnit.Pixel);

                        Color primaryColor = Color.LightGoldenrodYellow;
                        Color bgColor = Color.FromArgb(0, Color.SteelBlue);

                        var img = DrawTextWithResize(currentCharacter.ToString(), font, fontEmPoints, imageSize, primaryColor, bgColor, i);
                        
                        //TO-DO: Finalize the Em point reductions to keep entire character in-bounds
                        //var img = DrawTextWithPointAdjust(currentCharacter.ToString(), font, fontName, fontEmPoints, imageSize, primaryColor, bgColor, i);

                        if (img == null)
                        {
                            Console.WriteLine($"Exception creating image");
                            InvalidCharacterIndexes.Add(i);
                            continue;
                        }

                        if (imageSize == 16)
                        {
                            var imgSha256Hash = ComputeSha256HashForImage(img);

                            if (EmptyCharacterShaHashes.Contains(imgSha256Hash))
                            {
                                SkippedCharacterIndexes.Add(i);
                                continue;
                            }
                        }

                        if (writeEnabled)
                        {
                            img.Save(imgOutputFileName, OutputImageFormat);
                            writtenImages++;
                        }
                    }
                    currentBatch++;
                }
            }
        }

        private static void PopulateEnumeratedFontList()
        {
            using (InstalledFontCollection installedFonts = new InstalledFontCollection())
            {
                int i = 1;

                foreach (FontFamily fontFamily in installedFonts.Families)
                {
                    EnumeratedFontList.Add(i++, fontFamily.Name);
                }
            }
        }

        private static void DisplayInstalledFonts()
        {
            foreach (var installedFont in EnumeratedFontList)
            {
                Console.WriteLine($"{installedFont.Key}\t{installedFont.Value}");
            }
        }

        static string ComputeSha256HashForImage(Image img)
        {
            string sha256HashString = string.Empty;

            using (var ms = new MemoryStream())
            {
                img.Save(ms, ImageFormat.Png);

                using (SHA256 sha256Hash = SHA256.Create())
                {
                    byte[] bytes = sha256Hash.ComputeHash(ms.ToArray());

                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        builder.Append(bytes[i].ToString("x2"));
                    }
                    sha256HashString = builder.ToString();
                }
            }
            return sha256HashString;
        }

        //Parts of this method taken from here https://stackoverflow.com/questions/2070365/how-to-generate-an-image-from-text-on-fly-at-runtime
        private static Image DrawTextWithResize(string text, Font font, float emSize, int imageSize, Color textColor, Color backColor, int index = 0)
        {
            //first, create a dummy bitmap just to get a graphics object
            Image img = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(img);

            //measure the string to see how big the image will need to be
            SizeF textSize;

            try
            {
                textSize = drawing.MeasureString(text, font);
            }
            catch
            {
                return null;
            }

            //free up the dummy image and old graphics object
            img.Dispose();
            drawing.Dispose();

            var resizeImage = false;

            //As long as the text will fit within our imagesize, We're creating perfect squares
            //maybe for use in larger mosaics, so let the caller specify the imageSize
            //rather than measuring
            float fWidth = imageSize;
            float fHeight = imageSize;

            float fWidthRatioMultiplier;
            float fHeightRatioMultiplier;

            if (textSize.Width > imageSize || textSize.Height > imageSize)
            {
                fWidth = textSize.Width;
                fHeight = textSize.Height;

                fWidthRatioMultiplier = (float)imageSize / fWidth;
                fHeightRatioMultiplier = (float)imageSize / fHeight;

                resizeImage = true;
            }

            img = new Bitmap((int)fWidth, (int)fHeight);
            drawing = Graphics.FromImage(img);
            
            //paint the background
            drawing.Clear(backColor);

            //create a brush for the text
            Brush textBrush = new SolidBrush(textColor);
            
            //Antialias the text
            drawing.TextRenderingHint = TextRenderingHint.AntiAlias;

            try
            {
                drawing.DrawString(text, font, textBrush, 0, 0);
            }
            catch
            {
                return null;
            }

            drawing.Save();

            //Resize the image when the side of the text escaped the image bounds
            //TO-DO: Update the canvas size to square, then resize
            if (resizeImage)
            {
                var imageSizeBitmap = new Bitmap(imageSize, imageSize);

                var resizedGfx = Graphics.FromImage(imageSizeBitmap);

                resizedGfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                resizedGfx.DrawImage(img, 0, 0, imageSize, imageSize);

                textBrush.Dispose();
                resizedGfx.Dispose();
                drawing.Dispose();

                return imageSizeBitmap;
            }

            textBrush.Dispose();
            drawing.Dispose();

            return img;
        }

        //Parts of this method taken from here https://stackoverflow.com/questions/2070365/how-to-generate-an-image-from-text-on-fly-at-runtime
        private static Image DrawTextWithPointAdjust(string text, Font font, string fontName, float emSize, int imageSize, Color textColor, Color backColor, int index = 0)
        {
            //first, create a dummy bitmap just to get a graphics object
            Image img = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(img);

            //measure the string to see how big the image will need to be
            SizeF textSize;

            try
            {
                textSize = drawing.MeasureString(text, font);
            }
            catch
            {
                return null;
            }

            //free up the dummy image and old graphics object
            img.Dispose();
            drawing.Dispose();

            //As long as the text will fit within our imagesize, We're creating perfect squares
            //maybe for use in larger mosaics, so let the caller specify the imageSize
            //rather than measuring
            float fWidth = imageSize;
            float fHeight = imageSize;

            if (textSize.Width > imageSize || textSize.Height > imageSize)
            {
                float fontEmPoints = (float)emSize;
                while (true)
                {
                    // first, create a dummy bitmap just to get a graphics object
                    img = new Bitmap(1, 1);
                    drawing = Graphics.FromImage(img);

                    fontEmPoints = fontEmPoints - 1.0f;
                    
                    var resizedFont = new Font(fontName, fontEmPoints, FontStyle.Regular, GraphicsUnit.Pixel);

                    try
                    {
                        textSize = drawing.MeasureString(text, resizedFont);
                    }
                    catch
                    {
                        return null;
                    }

                    if (textSize.Width <= fWidth && textSize.Height <= fHeight)
                    {
                        break;
                    }
                }
            }

            img = new Bitmap((int)fWidth, (int)fHeight);
            drawing = Graphics.FromImage(img);

            //paint the background
            drawing.Clear(backColor);

            //create a brush for the text
            Brush textBrush = new SolidBrush(textColor);

            //Antialias the text
            drawing.TextRenderingHint = TextRenderingHint.AntiAlias;

            try
            {
                drawing.DrawString(text, font, textBrush, 0, 0);
            }
            catch
            {
                return null;
            }

            drawing.Save();

            textBrush.Dispose();
            drawing.Dispose();

            return img;
        }
    }
}
