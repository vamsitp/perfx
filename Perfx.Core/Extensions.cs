namespace Perfx
{
    using System;
    using System.IO;

    public static class Extensions
    {
        public static string GetFullPath(this string fileName)
        {
            if (fileName == null)
            {
                return null;
            }

            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nameof(Perfx), fileName);
        }
    }
}
