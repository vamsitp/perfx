namespace Perfx
{
    using System;
    using System.IO;

    public static class PathExtensions
    {
        public static string GetFullPath(this string fileName)
        {
            if (fileName == null)
            {
                return null;
            }

            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nameof(Perfx), fileName);
        }

        public static string GetFullPathEx(this string fileName, string extension = "json")
        {
            if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return fileName.GetFullPath();
            }

            return $"{fileName}.{extension}".GetFullPath();
        }
    }
}
