using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace FontDumpTruck
{
    class Program
    {
        static Dictionary<float, int> EmPointsImageSizeMap = new Dictionary<float, int>
        {
            { 15.0f, 16 },
            { 30.0f, 32 },
            { 60.0f, 64 },
            { 120.0f, 128 },
            { 240.0f, 256 },
        };

        static List<string> FontsToBot = new List<string>
        {
            "Cambria",
            "Wingdings",
            "Symbol"
        };

        static int asciiCharStart = 33;
        static int asciiCharStop = 255;

        static void Main(string[] args)
        {
            foreach (var fontName in FontsToBot)
            {
                foreach (var emPointAndImageSize in EmPointsImageSizeMap)
                {
                    var fontEmPoints = emPointAndImageSize.Key;
                    var imageSize = emPointAndImageSize.Value;

                    //this was a quick and super dirty test app, probably a better way to do this by enumerating all
                    //chars present in a .ttf
                    for (int i = asciiCharStart; i <= asciiCharStop; i++)
                    {
                        var character = (char)i;

                        Font font = new Font(fontName, fontEmPoints, FontStyle.Regular, GraphicsUnit.Pixel);
                        Color primaryColor = Color.LightGoldenrodYellow;
                        Color bgColor = Color.FromArgb(0, Color.SteelBlue);

                        var img = DrawText(character.ToString(), font, imageSize, primaryColor, bgColor);
                        var iHeight = img.Height;
                        var iWidth = img.Width;

                        var imgFormat = System.Drawing.Imaging.ImageFormat.Png;
                        var imgFormatFileSuffix = imgFormat.ToString().ToLower();

                        var imgOutputPath = $"{fontName}";

                        if (!Directory.Exists(imgOutputPath))
                        {
                            Directory.CreateDirectory(imgOutputPath);
                        }

                        var imgOutputFileName = fontName + "\\" + $"{fontName}_char-{i}_{iWidth}x{iHeight}.{imgFormatFileSuffix}";

                        if (File.Exists(imgOutputFileName))
                        {
                            File.Delete(imgOutputFileName);
                        }

                        img.Save(imgOutputFileName, imgFormat);
                        Console.WriteLine($"Saved {imgOutputFileName}");
                    }
                }
            }
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
            drawing.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            
            drawing.DrawString(text, font, textBrush, 0, 0);

            drawing.Save();

            textBrush.Dispose();
            drawing.Dispose();

            return img;
        }
    }
}
