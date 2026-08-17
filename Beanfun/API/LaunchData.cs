using System;
using System.Text;

namespace Beanfun
{
    static class LaunchData
    {
        private static readonly string[] Tables =
        {
            "bac987d65e432f10",
            "3bc4d5e6f2a79108",
            "cdbeaf9012456378",
            "4e6fb81a3c5d7092",
        };

        public static string DecodeLaunchTicket(string data)
        {
            string plaintext = Decode(data);
            if (plaintext == null)
                return null;

            string firstSegment = plaintext.Split(';')[0];
            foreach (string pair in firstSegment.Split('&'))
            {
                if (!pair.StartsWith("LaunchTicket=", StringComparison.Ordinal))
                    continue;
                string ticket = pair.Substring("LaunchTicket=".Length);
                if (ticket.Length == 64 && IsHex(ticket))
                    return ticket;
                return null;
            }
            return null;
        }

        private static string Decode(string data)
        {
            if (string.IsNullOrEmpty(data))
                return null;

            int selector;
            try
            {
                selector = Convert.ToInt32(data[0].ToString(), 16); // 首字 hex 選替代表
            }
            catch
            {
                return null;
            }

            string table = Tables[selector % Tables.Length];
            var normalized = new StringBuilder(data.Length - 1);
            for (int i = 1; i < data.Length; i++)
            {
                int idx = table.IndexOf(data[i]);
                if (idx < 0)
                    return null;
                normalized.Append(Convert.ToString(idx, 16));
            }

            int offset = selector + 1;
            if (normalized.Length < offset + 8)
                return null;

            string hex = normalized.ToString();
            string key = hex.Substring(offset, 8); // DES 金鑰 8 hex
            string cipherHex = hex.Substring(0, offset) + hex.Substring(offset + 8);
            return WCDESComp.DecryStrHex(cipherHex, key)?.Trim('\0');
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }
            return true;
        }
    }
}
