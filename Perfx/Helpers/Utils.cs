namespace Perfx
{
    using System;
    using System.IO;

    public static class Utils
    {
        public static string AuthInfoFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Perfx.json");
    }
}
