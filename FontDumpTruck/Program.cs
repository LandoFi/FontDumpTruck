using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Drawing.Text;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.ComponentModel;

namespace FontDumpTruck
{
    class Program
    {
        static HashSet<string> EmptyCharacterShaHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "2f070d7b8728981a31c17be423fd02119041809abf58bd2b98e2d96a4a71c874", //16x16 missing
            "7f5dd12b3ac95a278b3b3fba7942157805a607f805c560aa1102154207db9137", //32x32
            "2f36dd8e10c6228009d4a4ed7b5df7e2d9403a6b453a0608281339a143581c4b", //64x64
            "e44a743ba8f82bf074aa156674ab46dd797c368294f22e92eef25eed58fcc4d0", //128x128
            "9fbe14220853c0520dbed9e4b73ae0320d42eb3a5124b8eeae8bade474731a99", //256x256
            "5016a323018f09e292165ad5392d82dcbad5e79c2b6b93aff3322dffff80b309", //16x16 empty
            "eb64614220341e627440aaf334be808424b80151a64f73334b7c1bd0eabb08a8", //32x32
            "ca8686513b00f37317c2e702e08d4a4c57eed11f8e9ac2452a97b72b566c4690", //64x64
            "f4c65352dc319eeafe496f6dc06066b4cbaddac8b26ab8aac8320b73b0d08566", //128x128
            "088432a6533ce656cb2539424ed24ea27c5849395f75110e3d7fdd95f1654e97", //256x256
            "e0341dd152d792367abb61fe75c4fb7eec227e2ab3c3ef6388dc91bcae9aa25f", //16x16 missing zoomed
            "969d368195ef34bdb28c70b621bfd593f310294b6f563d69183087547c05de28", //32x32
            "71db026c967aa64e9e20d6555f091d9a9ea1186ba4d41c517e8bc2dfb4e239bb", //64x64
            "7b2a37d38f0029e885acc8997a340b3428aaf12a27a50970ca71e57fdf8de387", //128x128
            "d080d8883315f9961faf51595e968e292c573112bd7e7fdf3ee5622ecccd547e", //256x256
            "b3841228c51eaa713a6816abde9f9421079675186c76f82f38c2e57ba13c1ba7", //16x16 missing narrow
            "ebd2becb62aea2cf9fde15975596ffd3bc0579c2df10075c2a5db268cf31e5ab", //32x32
            "a2f2810a8749f9fda96ff56cb4aacae7ece3f5564962a15a74cdb80971d55947", //64x64
            "e5aca2a915b86c23f0c121504830e3e6af9d66884d4b75b7daf2ae80a2ad9eec", //128x128
            "c3ceb3688f2ba56e16559d5fe6f9ae2a290639e25fc8923017e0f02f68e89f21", //256x256
            "6f682a90d3d907fa5fac30ae6d2879d6b7f3bdee46e0fcd981b30e27ed6ee63f",
            "830edd12babf88b5d6ad768631d4b65044db0c9aabb090429ee6fdfcdcbcd662", //32x32 missing bold
            "49f5c43faedaac9529b0f200d7e1d36bb04ea9f878bb721cc706756e772ac713", //64x64
            "0374c5dc97fa6c36ee1c235bbac5468c7e4074b746eab577b51298a23658d876", //128x128
        };

        static Dictionary<float, int> EmPointsImageSizeMap = new Dictionary<float, int>
        {
            { 15.0f, 16 },
            //{ 30.0f, 32 },
            //{ 60.0f, 64 },
            //{ 120.0f, 128 },
            //{ 240.0f, 256 },
            { 480.0f, 512 },
            { 960.0f, 1024 }
        };

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

            var writtenImages = 0;
            var processedImages = 0;

            var totalSizeBatches = EmPointsImageSizeMap.Count;
            var currentBatch = 1;

            foreach (var fontName in FontsToDump)
            {
                var imgOutputPath = $"{fontName}";

                if (!Directory.Exists(imgOutputPath))
                {
                    Directory.CreateDirectory(imgOutputPath);
                }

                foreach (var emPointAndImageSize in EmPointsImageSizeMap)
                {
                    //this was a quick and super dirty test app, probably a better way to do this by enumerating all
                    //chars present in a .ttf
                    for (int i = asciiCharStart; i <= asciiCharStop; i++)
                    {
                        var fontEmPoints = emPointAndImageSize.Key;
                        var imageSize = emPointAndImageSize.Value;

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
                            File.Delete(imgOutputFileName);
                        }

                        Font font = new Font(fontName, fontEmPoints, FontStyle.Regular, GraphicsUnit.Pixel);

                        Color primaryColor = Color.LightGoldenrodYellow;
                        Color bgColor = Color.FromArgb(0, Color.SteelBlue);

                        var img = DrawText(currentCharacter.ToString(), font, imageSize, primaryColor, bgColor);

                        if (img == null)
                        {
                            InvalidCharacterIndexes.Add(i);
                            continue;
                        }

                        var imgSha256Hash = ComputeSha256HashForImage(img);

                        if (EmptyCharacterShaHashes.Contains(imgSha256Hash))
                        {
                            SkippedCharacterIndexes.Add(i);
                            continue;
                        }

                        img.Save(imgOutputFileName, OutputImageFormat);
                        writtenImages++;

                        //Console.WriteLine($"{imgSha256Hash}\tSaved {imgOutputFileName}");
                        //if (i == 7)
                        //{
                        //    break;
                        //}
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

        //Lots of this method taken from here https://stackoverflow.com/questions/2070365/how-to-generate-an-image-from-text-on-fly-at-runtime
        private static Image DrawText(string text, Font font, int imageSize, Color textColor, Color backColor)
        {
            //first, create a dummy bitmap just to get a graphics object
            Image img = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(img);

            //We're creating perfect squares -- maybe for use in larger mosaics, so let the caller specify the imageSize
            //rather than measuring
            //measure the string to see how big the image needs to be
            //SizeF textSize = drawing.MeasureString(text, font);

            //free up the dummy image and old graphics object
            img.Dispose();
            drawing.Dispose();

            //create a new image of the right size
            //img = new Bitmap((int)textSize.Width, (int)textSize.Height);
            img = new Bitmap(imageSize, imageSize);

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
            catch (Exception e)
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
