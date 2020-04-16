namespace Perfx
{
    using System;
    using System.IO;

    public static class PathExtensions
    {
        // https://yetanotherchris.dev/csharp/6-ways-to-get-the-current-directory-in-csharp/
        private static readonly string CurrDir = Path.Combine(Environment.CurrentDirectory, nameof(Perfx));
        private static readonly string MyDocsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nameof(Perfx));
        public static string BasePath { get; private set; } = Directory.Exists(CurrDir) ? CurrDir : MyDocsDir;

        public static string SetBasePath(this string inputPath)
        {
            if (inputPath != null)
            {
                // If the input is a File, and get its parent directory; else, assume it's a directory and use it as is
                var path = File.Exists(inputPath) ? Path.GetDirectoryName(Path.GetFullPath(inputPath)) : inputPath;
                if (Directory.Exists(path))
                {
                    // If the directory exists and the name is Perfx, use it as the base-folder
                    if (path.EndsWith(nameof(Perfx), StringComparison.OrdinalIgnoreCase))
                    {
                        BasePath = path;
                    }
                    else
                    {
                        // Check if Perfx folder exists in the directory; else, create it as it as the base-folder
                        path = Path.Combine(path, nameof(Perfx));
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        BasePath = path;
                    }
                }
            }

            return BasePath;
        }

        public static string GetFullPath(this string fileName)
        {
            if (fileName == null)
            {
                return null;
            }

            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(BasePath, fileName);
        }

        public static string GetFullPathEx(this string inputPath, string extension = "json")
        {
            var path = inputPath.GetFullPath();
            if (inputPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return Directory.Exists(inputPath) ? Path.Combine(path, nameof(Perfx) + ".Settings.json") : $"{inputPath}.{extension}".GetFullPath();
        }
    }
}
