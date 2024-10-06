using System;
using System.IO;
using System.Linq;
using ParsnipData.Media;
using ParsnipData;

namespace ParsnipMediaUploader
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Title = $"TheParsnipMediaUploader (Version {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version})";

            if (!Configuration.Initialize())
            {
                
                Console.ReadLine();
                return;
            }

            var logDir = $"{Configuration.DestinationDir}progress.log";
            var successLogDir = $"{Configuration.DestinationDir}success.log";
            var errorLogDir = $"{Configuration.DestinationDir}error.log";

            Helpers.OverwriteWrite($"Creating directories...");
            Directory.CreateDirectory($"{Configuration.DestinationDir}Originals");
            Directory.CreateDirectory($"{Configuration.DestinationDir}Compressed");
            Directory.CreateDirectory($"{Configuration.DestinationDir}Placeholders");
            Directory.CreateDirectory($"{Configuration.VideoDestinationDirOverride}Originals");
            
            Helpers.OverwriteWrite($"Sorting files by date...");
            var files = new DirectoryInfo(Configuration.SourceDir).GetFiles().OrderBy(p => p.DateTimeCreated());
            var totalFiles = files.Count();
            
            var errorCount = 0;
            var currentFile = 0;
            foreach (var file in files)
            {
                currentFile++;
                var fileName = file.Name;
                doProcessFile();

                void doProcessFile()
                {
                    try
                    {
                        Helpers.OverwriteWrite($"{currentFile}/{totalFiles} - (Processing {fileName}...)");

                        var originalExtension = file.Extension;
                        if (originalExtension == string.Empty)
                            throw new FileFormatException($"\"{fileName}\" has no extension.");
                        
                        var isImage = Image.IsValidFileExtension(originalExtension.Substring(1));
                        if (!isImage && !Video.IsValidFileExtension(originalExtension.Substring(1)))
                            throw new FileFormatException($"\"{originalExtension}\" is not a supported file type.");

                        if (file.Length == 0)
                            throw new InvalidDataException("The file is empty.");

                        var mediaId = MediaId.NewMediaId();
                        File.Copy(file.FullName, $"{(isImage ? Configuration.DestinationDir : Configuration.VideoDestinationDirOverride)}Originals\\{mediaId}{originalExtension}");

                        (isImage ? processImage() : processVideo()).InsertTags(Configuration.MediaTags);

                        var successLogMessage = $"\r[{DateTime.Now}] {fileName} - Success";
                        appendToLog(logDir, successLogMessage);
                        appendToLog(successLogDir, successLogMessage);

                        #region Subroutines
                        Media processImage()
                        {
                            var image = new Image()
                            {
                                Id = mediaId,
                                DateTimeCaptured = file.DateTimeCreated(),
                                CreatedById = Configuration.CreatedByUserId
                            };

                            Media.ProcessMediaThumbnail(image, image.Id.ToString(), originalExtension, Configuration.DestinationDir);

                            Helpers.OverwriteWrite($"{currentFile}/{totalFiles} - (Uploading {fileName}...)");
                            image.Upload(Configuration.DestinationDir, originalExtension);
                            Helpers.OverwriteWrite($"{currentFile}/{totalFiles} - (Inserting metadata for {fileName}...)");
                            if (!image.Insert()) throw new Exception("There was an exception whilst inserting the image into the database.");

                            return image;
                        }

                        Media processVideo()
                        {
                            var video = new Video()
                            {
                                Id = mediaId,
                                DateTimeCaptured = file.DateTimeCreated(),
                                CreatedById = Configuration.CreatedByUserId
                            };

                            Helpers.OverwriteWrite($"{currentFile}/{totalFiles} - (Uploading {fileName}...)");
                            video.Upload(Configuration.VideoDestinationDirOverride, originalExtension);

                            Helpers.OverwriteWrite($"{currentFile}/{totalFiles} - (Inserting metadata for {fileName}...)");
                            video.VideoData.OriginalFileDir = $"{video.VideoUploadsDir}Originals/{mediaId}{originalExtension}";
                            if (!video.Insert()) throw new Exception("There was an exception whilst inserting the video into the database."); ;

                            return video;
                        }
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        var exceptionLogMessage = $"\r[{DateTime.Now}] {fileName} - {ex.Message}";
                        appendToLog(logDir, exceptionLogMessage);
                        appendToLog(errorLogDir, exceptionLogMessage);
                        Helpers.OverwriteColorWriteLine($"There was an exception whilst processing {fileName}: {ex.Message}", ConsoleColor.Yellow);

                        if(Configuration.PauseOnException)
                            if(Helpers.GetBooleanResponse("Retry? (y/n)")) doProcessFile();
                        
                    }
                    void appendToLog(string dir, string message) =>
                        File.AppendAllText(dir, $"{(File.Exists(dir) ? string.Empty : "\r")}{message}");
                }
            }

            var finalMessage = $"{currentFile}/{totalFiles} - Done";
            ConsoleColor finalMessageColor;
            if (errorCount > 0)
            {
                finalMessageColor = ConsoleColor.Red;
                finalMessage += $" with {errorCount} error{(errorCount > 1 ? "s" : "")} (See {logDir})";
            }
            else finalMessageColor = ConsoleColor.Green;

            Helpers.OverwriteColorWriteLine(finalMessage, finalMessageColor);
            Console.ReadLine();
        }
    }
}
