using BugTracker.Services.Interfaces;

namespace BugTracker.Services
{
    public class BTFileService : IBTFileService
    {
        private readonly string[] suffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB" };

        public string ConvertByteArrayToFile(byte[] filedata, string extension)
        {
            try
            {
                string imageBase64Data = Convert.ToBase64String(filedata);
                return string.Format($"data:{extension};base64,{imageBase64Data}");
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<byte[]> ConvertFileToByteArrayAsync(IFormFile file)
        {
            try
            {
                MemoryStream memoryStream = new();
                await file.CopyToAsync(memoryStream);
                byte[] bytefile = memoryStream.ToArray();
                memoryStream.Close();
                memoryStream.Dispose();

                return bytefile;

            }
            catch (Exception)
            {
                throw;
            }
        }

        public string FormatFileSize(long bytes)
        {
            int counter = 0;
            decimal fileSize = bytes;
            while (Math.Round(fileSize / 1024) >= 1 )
            {
                fileSize /= bytes;
                counter++;
            }
            return string.Format("{0:n1}{1}",fileSize, suffixes[counter]);
        }

        public string GetFileIcon(string file)
        {
            string ext = (!string.IsNullOrWhiteSpace(file)) ? Path.GetExtension(file).Replace(".", "") : "default";
            return $"/img/contenttype/{ext}.png";
        }
    }
}
