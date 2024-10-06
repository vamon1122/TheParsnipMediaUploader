using ParsnipData.Media;
using System;
using static System.Net.WebRequestMethods;
using System.IO;
using System.Net;

namespace ParsnipMediaUploader
{
    public static class Helpers
    {
        public static string GetResponse(string prompt)
        {
            Console.Write($"\r{string.Empty.LengthenTo(100)}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"\r{prompt}: ");
            Console.ResetColor();
            Console.CursorVisible = true;
            var response = Console.ReadLine();

            return response;
        }

        public static void FtpUpload(Media media, string localDir, string remoteDir, string folder, string extension)
        {
            var ftpClient = (FtpWebRequest)WebRequest.Create($"{Configuration.FtpUrl}/{Configuration.Website}/wwwroot/{remoteDir}/{folder}/{media.Id}{extension}");
            ftpClient.Credentials = Configuration.FtpCredentials;
            ftpClient.Method = Ftp.UploadFile;
            ftpClient.UseBinary = true;
            ftpClient.KeepAlive = true;
            var fi = new FileInfo($"{localDir}\\{folder}\\{media.Id}{extension}");
            ftpClient.ContentLength = fi.Length;
            byte[] buffer = new byte[4097];
            int bytes;
            var total_bytes = (int)fi.Length;
            try
            {
                using (FileStream fs = fi.OpenRead())
                {
                    using (Stream rs = ftpClient.GetRequestStream())
                    {
                        while (total_bytes > 0)
                        {
                            bytes = fs.Read(buffer, 0, buffer.Length);
                            rs.Write(buffer, 0, bytes);
                            total_bytes -= bytes;
                        }
                    }
                }
            }
            catch
            {
                throw new WebException("Upload failed.");
            }
        }

        public static bool GetBooleanResponse(string prompt)
        {
            var response = GetResponse(prompt).Substring(0, 1).ToLower();
            if (response == "y") return true;
            if (response == "n") return false;
            return GetBooleanResponse(prompt);
        }
        
        public static void OverwriteColorWriteLine(string str, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"\r{str.LengthenTo(100)}");
            Console.ResetColor();
            Console.CursorVisible = false;
        }

        public static void OverwriteWrite(string str)
        {
            Console.Write($"\r{str.LengthenTo(100)}");
            Console.CursorVisible = false;
        }
    }
}
