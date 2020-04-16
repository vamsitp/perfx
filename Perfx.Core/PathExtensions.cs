namespace Perfx
{
    using System;
    using System.IO;

    public static class PathExtensions
    {
        // https://yetanotherchris.dev/csharp/6-ways-to-get-the-current-directory-in-csharp/
        private static readonly string CurrDir = Path.Combine(Environment.CurrentDirectory, nameof(Perfx));
        private static readonly string MyDocsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nameof(Perfx));

        public static string GetFullPath(this string fileName)
        {
            if (fileName == null)
            {
                return null;
            }

            var root = Directory.Exists(CurrDir) ? CurrDir : MyDocsDir;
            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(root, fileName);
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
