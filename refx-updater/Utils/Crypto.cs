using System;
using System.IO;
using System.Security.Cryptography;

namespace refx_updater.Utils
{
    public static class Crypto
    {
        internal static string GetMd5Hash(string filename)
        {
            using (var stream = File.OpenRead(filename))
            using (var md5 = MD5.Create())
            {
                var checksum = md5.ComputeHash(stream);
                
                return BitConverter.ToString(checksum).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}