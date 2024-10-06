using ParsnipData.Media;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;

namespace ParsnipMediaUploader
{
    internal static class Configuration
    {
        private static readonly string createdByUserId = ConfigurationManager.AppSettings["CreatedByUserId"];
        private static readonly string destinationDir = ConfigurationManager.AppSettings["DestinationDir"];
        private static readonly string pauseOnException = ConfigurationManager.AppSettings["PauseOnException"];
        private static readonly string sourceDir = ConfigurationManager.AppSettings["SourceDir"];
        private static readonly string tags = ConfigurationManager.AppSettings["Tags"];
        private static readonly string testConnections = ConfigurationManager.AppSettings["TestConnections"];
        private static readonly string uploadOriginalImages = ConfigurationManager.AppSettings["UploadOriginalImages"];
        private static readonly string videoDestinationDir = ConfigurationManager.AppSettings["VideoDestinationDirOverride"];

        public static readonly NetworkCredential FtpCredentials = new NetworkCredential(ConfigurationManager.AppSettings["FtpUsername"], ConfigurationManager.AppSettings["FtpPassword"]);
        public static readonly string FtpUrl = ConfigurationManager.AppSettings["FtpUrl"];
        public static readonly string RemoteImagesDir = ConfigurationManager.AppSettings["RemoteImagesDir"];
        public static readonly string RemoteVideosDir = ConfigurationManager.AppSettings["RemoteVideosDir"];
        public static readonly string Website = ConfigurationManager.AppSettings["WebsiteUrl"];
        
        public static int CreatedByUserId { get; private set; }
        public static string DestinationDir { get; private set; }
        public static List<MediaTag> MediaTags { get; private set; }
        public static bool PauseOnException { get; private set; }
        public static string SourceDir { get; private set; }
        public static bool UploadOriginalImages { get; private set; }
        public static string VideoDestinationDirOverride { get; private set; }

        public static bool Initialize() 
        {
            PauseOnException = getTrueFalse("Pause on exception", pauseOnException);

            if (getTrueFalse("Test connections", testConnections))
            {
                validateRemoteConfiguration();
            }
            
            CreatedByUserId = getInt("Created by userId", createdByUserId);
            DestinationDir = getDestination("Backup destination", destinationDir) ?? SourceDir.EnsureEndsWith('\\');
            MediaTags = getMediaTags();
            SourceDir = getSource();
            UploadOriginalImages = getTrueFalse("Upload original images", uploadOriginalImages);
            VideoDestinationDirOverride = getDestination("Video backup destination override", videoDestinationDir) ?? DestinationDir;

            return true;

            #region Subroutines
            bool getTrueFalse(string prompt, string configValue)
            {
                if (bool.TryParse(configValue, out bool configValueAsBool)) return configValueAsBool;

                return getResponse();

                bool getResponse()
                {
                    var response = Helpers.GetResponse($"{prompt}? (y/n)").Substring(0, 1).ToLower();
                    if (response == "y") return true;
                    if (response == "n") return false;
                    return getResponse();
                }
            }

            void validateRemoteConfiguration()
            {
                Helpers.OverwriteWrite("Validating remote configuration...");

                var canConnectToFTP = checkConnectionToFTP();
                var imageOriginalsExists = checkFTPFolderExists($"{RemoteImagesDir}/Originals");
                var imageCompressedExists = checkFTPFolderExists($"{RemoteImagesDir}/Compressed");
                var imagePlaceholdersExists = checkFTPFolderExists($"{RemoteImagesDir}/Placeholders");
                var videoOriginalsExists = checkFTPFolderExists($"{RemoteVideosDir}/Originals");
                var canConnectToDB = checkConnectionToDatabase();

                var errors = 0;
                if (!canConnectToFTP) errors++;
                if (!imageOriginalsExists) errors++;
                if (!imageCompressedExists) errors++;
                if (!imagePlaceholdersExists) errors++;
                if (!videoOriginalsExists) errors++;
                
                if (errors > 0)
                {
                    var isMultipleErrors = errors > 1;
                    var error = $"The remote configuration appears to be invalid{(isMultipleErrors ? ":" : "; ")}";
                    var prefix = isMultipleErrors ? "\n - " : string.Empty;
                    
                    if (!canConnectToFTP) error += $"{prefix}Unable to connect to configured FTP server";
                    else
                    {
                        var e = "folder does not exist on the remote";
                        if (!imageOriginalsExists)
                        {
                            error += $"{prefix}Originals image {e}";
                        }

                        if (!imageCompressedExists) error += $"{prefix}Compressed image {e}";
                        if (!imagePlaceholdersExists) error += $"{prefix}Placeholders image {e}";
                        if (!videoOriginalsExists) error += $"{prefix}Originals video {e}";
                    }

                    if (!canConnectToDB) error += ($"{prefix}Unable to connect to configured database server", ConsoleColor.Red);

                    Helpers.OverwriteColorWriteLine(error, ConsoleColor.Red);
                    if (PauseOnException)
                        if (Helpers.GetBooleanResponse("Re-test? (y/n)"))
                            validateRemoteConfiguration();
                }

                bool checkConnectionToFTP()
                {
                    try
                    {
                        var request = (FtpWebRequest)WebRequest.Create(FtpUrl);
                        request.Method = WebRequestMethods.Ftp.ListDirectory;
                        request.Credentials = FtpCredentials;
                        using (var response = (FtpWebResponse)request.GetResponse()) { return response.StatusCode == FtpStatusCode.OpeningData || response.StatusCode == FtpStatusCode.DataAlreadyOpen; }
                    }
                    catch
                    {
                        return false;
                    }
                }

                bool checkFTPFolderExists(string remoteDir)
                {
                    try
                    {
                        string fullPath = $"{FtpUrl}/{Website}/wwwroot/{remoteDir}";
                        FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fullPath);
                        request.Method = WebRequestMethods.Ftp.ListDirectory;
                        request.Credentials = FtpCredentials;

                        using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                        {
                            return response.StatusCode == FtpStatusCode.OpeningData ||
                                response.StatusCode == FtpStatusCode.DataAlreadyOpen;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }

                bool checkConnectionToDatabase()
                {
                    try
                    {
                        using (var connection = new SqlConnection(ParsnipData.Parsnip.ParsnipConnectionString))
                        {
                            connection.Open();
                            return connection.State == System.Data.ConnectionState.Open;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            int getInt(string prompt, string configValue)
            {
                if (int.TryParse(configValue, out var configValueAsInt))
                    return configValueAsInt;

                return getResponse();

                int getResponse()
                {
                    if (int.TryParse(Helpers.GetResponse($"{prompt}").Substring(0, 1).ToLower(), out var responseAsInt))
                        return responseAsInt;

                    return getResponse();
                }
            }

            string getDestination(string prompt, string configDestinationDir)
            {
                var destination = getDirectory(prompt, configDestinationDir);
                if (string.IsNullOrWhiteSpace(destination)) return null;

                destination = containsDriveLetter(destination) ? destination : SourceDir.EnsureEndsWith('\\') + destination;
                try
                {
                    Directory.CreateDirectory(destination);
                }
                catch
                {
                    destination = getDirectory(prompt);
                }

                return destination.EnsureEndsWith('\\');

                bool containsDriveLetter(string directory) => directory.Contains(":\\");
            }

            List<MediaTag> getMediaTags()
            {
                var config = Configuration.tags;
                var tags = new List<MediaTag>();
                var tagsString = (config.Length > 0 ? config : Helpers.GetResponse("Tags (comma separated)")).Replace("#", "");
                if (!string.IsNullOrWhiteSpace(tagsString))
                    tagsString.Split(',').ToList().ForEach(t => tags.Add(new MediaTag(t.Trim())));

                return tags;
            }

            string getSource()
            {
                var dir = getDirectory("Source", sourceDir);
                if (!Directory.Exists(dir)) return getSource();
                return dir;
            }

            string getDirectory(string prompt, string configDir = null)
            {
                if (!string.IsNullOrWhiteSpace(configDir)) return configDir;

                return getDirectoryFromUser();

                string getDirectoryFromUser()
                {
                    var response = Helpers.GetResponse(prompt);
                    return response;
                }
            }
            #endregion
        }
    }
}
