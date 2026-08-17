using System;
using System.Security.Cryptography;
using System.Text;

namespace Beanfun
{
    class WCDESComp
    {
        public static string DecryStrHex(string hexString, string key)
        {
            try
            {
                using DES des = DES.Create();
                des.Mode = CipherMode.ECB; // beanfun OTP 用 DES-ECB
                des.Padding = PaddingMode.None;
                des.Key = Encoding.ASCII.GetBytes(key);
                byte[] byteOUT = new byte[hexString.Length / 2];
                for (int i = 0; i < hexString.Length; i += 2)
                {
                    byteOUT[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
                }
                ICryptoTransform desdecrypt = des.CreateDecryptor();
                return Encoding.ASCII.GetString(
                    desdecrypt.TransformFinalBlock(byteOUT, 0, byteOUT.Length)
                );
            }
            catch (Exception e)
            {
                Console.WriteLine("DecryptDESError:" + e.Message + "\n" + e.StackTrace);
                return null;
            }
        }
    }
}
