using ParsnipData.Media;
using System.Collections.Generic;

namespace ParsnipMediaUploader
{
    public static class Extensions
    {
        private static bool EndsWith(this string str, char ch) =>
           str.Substring(str.Length - 1, 1)[0] == ch;

        public static string EnsureEndsWith(this string str, char ch) =>
            str.EndsWith(ch) ? str : str + ch;

        public static void InsertTags(this Media media, List<MediaTag> tags)
        {
            foreach (var tag in tags)
            {
                var pair = new MediaTagPair(media, tag, 1);
                pair.Insert();
            }
        }

        public static string LengthenTo(this string str, int length)
        {
            var spaces = length - str.Length;
            for (var x = 1; x < spaces; x++) str += " ";
            return str;
        }

        public static void Upload(this Image image, string LocalThumbnailsDir, string originalExt)
        {
            if (Configuration.UploadOriginalImages)
                Helpers.FtpUpload(image, LocalThumbnailsDir, Configuration.RemoteImagesDir, "Originals", originalExt);
            
            Helpers.FtpUpload(image, LocalThumbnailsDir, Configuration.RemoteImagesDir, "Compressed", ".jpg");
            Helpers.FtpUpload(image, LocalThumbnailsDir, Configuration.RemoteImagesDir, "Placeholders", ".jpg");
        }

        public static void Upload(this Video video, string localVideosDir, string originalExt) =>
            Helpers.FtpUpload(video, localVideosDir, Configuration.RemoteVideosDir, "Originals", originalExt);
    }
}
