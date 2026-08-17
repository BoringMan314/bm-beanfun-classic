// 多帳號紀錄的加解密儲存（JSON；舊檔為 BinaryFormatter）
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Utility.ModifyRegistry;

namespace Beanfun
{
    [Serializable]
    class AccountRecords
    {
        public List<string> regionList = null,
            accountList = null,
            passwdList = null,
            verifyList = null;
        public List<int> methodList = null;
        public List<bool> autoLoginList = null;
    }

    [Serializable]
    class Records
    {
        public List<string> regionList = null,
            accountList = null,
            accountNameList = null,
            passwdList = null,
            verifyList = null;
        public List<int> methodList = null;
        public List<bool> autoLoginList = null;

        public static Records Change(object oldRecords)
        {
            Records res = new Records();
            if (oldRecords is AccountRecords)
            {
                AccountRecords records = (AccountRecords)oldRecords;
                res.regionList = records.regionList;
                res.accountList = records.accountList;
                res.passwdList = records.passwdList;
                res.verifyList = records.verifyList;
                res.methodList = records.methodList;
                res.autoLoginList = records.autoLoginList;
            }
            return res;
        }
    }

    public class AccountManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(
            typeof(AccountManager)
        );

        private Records accountRecords = null;
        private string dataPath =
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData)
            + "\\BeanfunClassic\\Users.dat";

        public bool init()
        {
            return loadRecord();
        }

        #region 輔助方法
        private void accRecInit()
        {
            if (accountRecords == null)
                accountRecords = new Records();

            if (accountRecords.accountList == null)
                accountRecords.accountList = new List<string>();

            if (accountRecords.regionList == null)
                accountRecords.regionList = new List<string>();
            if (accountRecords.regionList.Count < accountRecords.accountList.Count)
            {
                for (
                    int i = accountRecords.regionList.Count;
                    i < accountRecords.accountList.Count;
                    i++
                )
                {
                    accountRecords.regionList.Add("TW");
                }
            }

            if (accountRecords.accountNameList == null)
                accountRecords.accountNameList = new List<string>();
            if (accountRecords.accountNameList.Count < accountRecords.accountList.Count)
            {
                for (
                    int i = accountRecords.accountNameList.Count;
                    i < accountRecords.accountList.Count;
                    i++
                )
                {
                    accountRecords.accountNameList.Add("");
                }
            }

            if (accountRecords.passwdList == null)
                accountRecords.passwdList = new List<string>();
            if (accountRecords.passwdList.Count < accountRecords.accountList.Count)
            {
                for (
                    int i = accountRecords.passwdList.Count;
                    i < accountRecords.accountList.Count;
                    i++
                )
                {
                    accountRecords.passwdList.Add("");
                }
            }

            if (accountRecords.verifyList == null)
                accountRecords.verifyList = new List<string>();
            if (accountRecords.verifyList.Count < accountRecords.accountList.Count)
            {
                for (
                    int i = accountRecords.verifyList.Count;
                    i < accountRecords.accountList.Count;
                    i++
                )
                {
                    accountRecords.verifyList.Add("");
                }
            }

            if (accountRecords.methodList == null)
                accountRecords.methodList = new List<int>();
            if (accountRecords.methodList.Count < accountRecords.accountList.Count)
            {
                for (
                    int i = accountRecords.methodList.Count;
                    i < accountRecords.accountList.Count;
                    i++
                )
                {
                    accountRecords.methodList.Add(0);
                }
            }

            if (accountRecords.autoLoginList == null)
                accountRecords.autoLoginList = new List<bool>();
            if (accountRecords.autoLoginList.Count < accountRecords.accountList.Count)
            {
                for (
                    int i = accountRecords.autoLoginList.Count;
                    i < accountRecords.accountList.Count;
                    i++
                )
                {
                    accountRecords.autoLoginList.Add(false);
                }
            }
        }

        private bool loadRecord()
        {
            var raw = readRawData();
            if (raw != null)
            {
                try
                {
                    accountRecords = JsonConvert.DeserializeObject<Records>(raw); // 新版 JSON
                }
                catch
                {
                    accountRecords = null;
                    TryAutoMigrateLegacyData(raw); // JSON 失敗則當舊版 BinaryFormatter
                }
            }
            accRecInit();

            return true;
        }

        private bool storeRecord()
        {
            string json = JsonConvert.SerializeObject(accountRecords);
            writeRawData(json);
            return true;
        }
        #endregion

        #region 帳號檔讀寫
        private string readRawData() // 讀出並解密帳號檔
        {
            try
            {
                if (File.Exists(dataPath))
                {
                    try
                    {
                        Byte[] cipher = File.ReadAllBytes(dataPath);
                        ModifyRegistry myRegistry = new ModifyRegistry();
                        myRegistry.BaseRegistryKey = Microsoft.Win32.Registry.CurrentUser;
                        string entropy = myRegistry.Read("Entropy");
                        byte[] plaintext = ProtectedData.Unprotect(
                            cipher,
                            Encoding.UTF8.GetBytes(entropy),
                            DataProtectionScope.CurrentUser
                        );
                        return Encoding.UTF8.GetString(plaintext);
                    }
                    catch
                    {
                        File.Delete(dataPath);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void writeRawData(string plaintext) // 加密後寫入帳號檔
        {
            using (BinaryWriter writer = new BinaryWriter(File.Open(dataPath, FileMode.Create)))
            {
                var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var random = new Random();
                string entropy = new string(
                    Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray()
                );

                ModifyRegistry myRegistry = new ModifyRegistry();
                myRegistry.BaseRegistryKey = Microsoft.Win32.Registry.CurrentUser;
                myRegistry.Write("Entropy", entropy);

                writer.Write(ciphertext(plaintext, entropy));
            }
        }

        private byte[] ciphertext(string plaintext, string key)
        {
            byte[] plainByte = Encoding.UTF8.GetBytes(plaintext);
            byte[] entropy = Encoding.UTF8.GetBytes(key);
            return ProtectedData.Protect(plainByte, entropy, DataProtectionScope.CurrentUser);
        }
        #endregion

        #region 對外介面
        public bool addAccount(
            string region,
            string account,
            string name,
            string password,
            string verify,
            int method,
            bool autoLogin
        )
        {
            return addAccount(-1, region, account, name, password, verify, method, autoLogin);
        }

        public bool addAccount(
            int index,
            string region,
            string account,
            string name,
            string password,
            string verify,
            int method,
            bool autoLogin
        )
        {
            bool isExists = false;
            List<int> regionIndex = new List<int>();
            for (int i = 0; i < accountRecords.accountList.Count; ++i)
            {
                if (region != accountRecords.regionList[i])
                {
                    continue;
                }
                if (account == accountRecords.accountList[i])
                {
                    if (index > -1 && regionIndex.Count != index)
                    {
                        removeAccount(region, account);
                        i--;
                        continue;
                    }
                    accountRecords.accountNameList[i] = name;
                    accountRecords.passwdList[i] = password;
                    accountRecords.verifyList[i] = verify;
                    accountRecords.methodList[i] = method;
                    accountRecords.autoLoginList[i] = autoLogin;
                    isExists = true;
                    break;
                }
                regionIndex.Add(i);
            }

            if (!isExists)
            {
                if (index < 0 || regionIndex.Count <= index)
                {
                    accountRecords.regionList.Add(region);
                    accountRecords.accountList.Add(account);
                    accountRecords.accountNameList.Add(name);
                    accountRecords.passwdList.Add(password);
                    accountRecords.verifyList.Add(verify);
                    accountRecords.methodList.Add(method);
                    accountRecords.autoLoginList.Add(autoLogin);
                }
                else
                {
                    index = regionIndex[index];
                    accountRecords.regionList.Insert(index, region);
                    accountRecords.accountList.Insert(index, account);
                    accountRecords.accountNameList.Insert(index, name);
                    accountRecords.passwdList.Insert(index, password);
                    accountRecords.verifyList.Insert(index, verify);
                    accountRecords.methodList.Insert(index, method);
                    accountRecords.autoLoginList.Insert(index, autoLogin);
                }
            }

            storeRecord();

            return true;
        }

        public string getNameByAccount(string region, string account)
        {
            for (int i = 0; i < accountRecords.accountList.Count; ++i)
            {
                if (
                    account == accountRecords.accountList[i]
                    && region == accountRecords.regionList[i]
                )
                {
                    return accountRecords.accountNameList[i];
                }
            }
            return null;
        }

        public string getPasswordByAccount(string region, string account)
        {
            for (int i = 0; i < accountRecords.accountList.Count; ++i)
            {
                if (
                    account == accountRecords.accountList[i]
                    && region == accountRecords.regionList[i]
                )
                {
                    return accountRecords.passwdList[i];
                }
            }
            return null;
        }

        public string getVerifyByAccount(string region, string account)
        {
            for (int i = 0; i < accountRecords.accountList.Count; ++i)
            {
                if (
                    account == accountRecords.accountList[i]
                    && region == accountRecords.regionList[i]
                )
                {
                    return accountRecords.verifyList[i];
                }
            }
            return null;
        }

        public int getMethodByAccount(string region, string account)
        {
            for (int i = 0; i < accountRecords.accountList.Count; ++i)
            {
                if (
                    account == accountRecords.accountList[i]
                    && region == accountRecords.regionList[i]
                )
                {
                    return accountRecords.methodList[i];
                }
            }
            return -1;
        }

        public bool getAutoLoginByAccount(string region, string account)
        {
            for (int i = 0; i < accountRecords.accountList.Count; ++i)
            {
                if (
                    account == accountRecords.accountList[i]
                    && region == accountRecords.regionList[i]
                )
                {
                    return accountRecords.autoLoginList[i];
                }
            }
            return false;
        }

        public bool removeAccount(string region, string account)
        {
            for (int i = 0; i < accountRecords.accountList.Count; ++i)
            {
                if (
                    account == accountRecords.accountList[i]
                    && region == accountRecords.regionList[i]
                )
                {
                    accountRecords.regionList.RemoveAt(i);
                    accountRecords.accountList.RemoveAt(i);
                    accountRecords.accountNameList.RemoveAt(i);
                    accountRecords.passwdList.RemoveAt(i);
                    accountRecords.verifyList.RemoveAt(i);
                    accountRecords.methodList.RemoveAt(i);
                    accountRecords.autoLoginList.RemoveAt(i);

                    storeRecord();
                    return true;
                }
            }
            return false;
        }

        public string[] getAccountList(string region)
        {
            List<string> accList = new List<string>();
            for (int i = 0; i < accountRecords.accountList.Count; ++i)
            {
                if (region == accountRecords.regionList[i])
                {
                    accList.Add(accountRecords.accountList[i]);
                }
            }
            return accList.ToArray();
        }

        public bool importRecord(string raw)
        {
            try
            {
                accountRecords = JsonConvert.DeserializeObject<Records>(raw);
                accRecInit();
                storeRecord();
                return true;
            }
            catch
            {
                return TryAutoMigrateLegacyData(raw); // 匯入 JSON 失敗則轉舊版
            }
        }

        public string exportRecord()
        {
            return JsonConvert.SerializeObject(accountRecords);
        }
        #endregion

        #region 舊版格式轉換
        private bool TryAutoMigrateLegacyData(string raw) // 將舊版 BinaryFormatter 帳號檔升成 JSON
        {
            try
            {
                byte[] cipher;
                try
                {
                    cipher = Convert.FromBase64String(raw);
                }
                catch (FormatException)
                {
                    return false;
                }
                using (var stream = new MemoryStream(cipher))
                {
#pragma warning disable SYSLIB0011 // 僅此處讀舊版 BinaryFormatter
                    var bformatter =
                        new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    object oldRecords = bformatter.Deserialize(stream);
#pragma warning restore SYSLIB0011

                    if (oldRecords != null)
                    {
                        string tempJson = JsonConvert.SerializeObject(oldRecords); // 經 JSON 避開舊型別轉型
                        accountRecords = JsonConvert.DeserializeObject<Records>(tempJson);

                        if (accountRecords != null)
                        {
                            accRecInit();
                            storeRecord(); // 立刻以 JSON 覆寫舊檔

                            log.Info("Legacy account data auto-migrated to JSON format.");
                            System.Windows.MessageBox.Show(
                                System.Windows.Application.Current.TryFindResource(
                                    "LegacyDataMigrateSuccess"
                                ) as string,
                                System.Windows.Application.Current.TryFindResource(
                                    "LegacyDataMigrateTitle"
                                ) as string,
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information
                            );
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Auto-migration of legacy data failed: {ex.Message}"); // 轉換失敗則交由 accRecInit 建空白紀錄
            }

            return false;
        }
        #endregion
    }
}
