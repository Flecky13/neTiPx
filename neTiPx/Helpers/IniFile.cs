using System.Runtime.InteropServices;
using System.Text;

namespace neTiPx.Helpers
{
    public static class IniFile
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder result, int size, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);

        public static string Read(string section, string key, string defaultValue, string path)
        {
            var result = new StringBuilder(255);
            GetPrivateProfileString(section, key, defaultValue, result, 255, path);
            return result.ToString();
        }

        public static void Write(string section, string key, string value, string path)
        {
            WritePrivateProfileString(section, key, value, path);
        }
    }
}
