using System;
using System.Windows;
using Microsoft.Win32;

namespace Utility.ModifyRegistry
{
    class ModifyRegistry
    {
        private string subKey =
            "SOFTWARE\\" + Application.ResourceAssembly.GetName().Name.ToUpper();

        public string SubKey
        {
            get { return subKey; }
            set { subKey = value; }
        }

        private RegistryKey baseRegistryKey = Registry.LocalMachine;

        public RegistryKey BaseRegistryKey
        {
            get { return baseRegistryKey; }
            set { baseRegistryKey = value; }
        }

        public string Read(string KeyName)
        {
            RegistryKey rk = baseRegistryKey;
            RegistryKey sk1 = rk.OpenSubKey(subKey);
            if (sk1 == null)
                return null;
            try
            {
                return (string)sk1.GetValue(KeyName.ToUpper());
            }
            catch
            {
                return null;
            }
        }

        public bool Write(string KeyName, object Value)
        {
            try
            {
                RegistryKey rk = baseRegistryKey;
                RegistryKey sk1 = rk.CreateSubKey(subKey);
                sk1.SetValue(KeyName.ToUpper(), Value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
