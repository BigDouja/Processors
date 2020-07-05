using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using System.IO;
using System.Drawing;


namespace Image_Processor
{
    class ImageJSONEntry
    {
        public string MainImgFilename { get; set; }
        public string ThumbnailFilename { get; set; }
    }

    class Program
    {
        static bool ThumbnailCallback()
        {
            return false;
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    foreach (var pictureLocation in args.ToList())
                    {
                        var imageCollection = new List<ImageJSONEntry>();

                        Console.WriteLine($"processing pictures in {pictureLocation}{Environment.NewLine}");

                        foreach (var filename in Directory.GetFiles(pictureLocation))
                        {
                            if (Path.GetFileName(filename).StartsWith("thumbnail_", StringComparison.CurrentCultureIgnoreCase))
                            {
                                continue;
                            }

                            try
                            {
                                var image = Image.FromFile(filename);
                                var thbnailFilename = Path.Combine(Path.GetDirectoryName(filename), $"thumbnail_{Path.GetFileName(filename)}");

                                if (File.Exists(thbnailFilename))
                                {
                                    Console.WriteLine($"thumbnail exists for {Path.GetFileName(filename)}");
                                }
                                else
                                {
                                    var thbnail = image.GetThumbnailImage(50, 50, new Image.GetThumbnailImageAbort(ThumbnailCallback), IntPtr.Zero);
                                    
                                    thbnail.Save(thbnailFilename);
                                    Console.WriteLine($"Saved {Path.GetFileName(thbnailFilename)}");
                                }

                                imageCollection.Add(new ImageJSONEntry() { MainImgFilename = filename, ThumbnailFilename = thbnailFilename });
                            }
                            catch (Exception)
                            {
                                Console.WriteLine($"{Path.GetFileName(filename)} is not an image.");
                            }
                        }

                        var collFilename = $"{(new DirectoryInfo(Path.GetDirectoryName(pictureLocation))).Name}.json";

                        if ( imageCollection.Count > 0)
                        {

                        }
                    }
                }
                else
                {
                    Console.WriteLine("arguments is empty");
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine($"Fatal Exception Occurred: {exc.ToString()}");
            }
            finally
            {
                Console.WriteLine($"{Environment.NewLine}Done processing images.");
                Console.ReadLine();
            }
        }
    }
}
