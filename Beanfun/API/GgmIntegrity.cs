using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace Beanfun
{
    class GgmIntegrity
    {
        private const string BuiltinCv = "1.5.0.2";
        private const string BuiltinHash =
            "dfd568a69d87abcd8f4a93d1a4481ebb57712d1d28ab0b6fc018fcf140101e06";
        private const string BuiltinArch = "x64";
        private const string DllName = "GGMWebStart.dll";
        private const string GitHubJsonUrl =
            "https://raw.githubusercontent.com/BoringMan314/bm-beanfun-classic/code/ggm-integrity.json";

        public string Cv { get; }
        public string Hash { get; }
        public string Arch { get; }

        private GgmIntegrity(string cv, string hash, string arch)
        {
            Cv = cv;
            Hash = hash;
            Arch = arch;
        }

        public static GgmIntegrity Builtin()
        {
            return new GgmIntegrity(BuiltinCv, BuiltinHash, BuiltinArch); // 伺服器接受的已知 GGM 身分
        }

        public bool SameAs(GgmIntegrity other)
        {
            return other != null && Cv == other.Cv && Hash == other.Hash && Arch == other.Arch;
        }

        public static GgmIntegrity TryFromGitHub()
        {
            try
            {
                return ParseJson(Update.ApplicationUpdater.TryDownloadGitHubText(GitHubJsonUrl)); // 倉庫 ggm-integrity.json
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GgmIntegrity] GitHub fetch failed: {ex.Message}");
                return null;
            }
        }

        internal static GgmIntegrity TryFromLocalDll()
        {
            try
            {
                string dll = LocateGgmDll();
                if (dll == null)
                    return null;

                string hash = Sha256LowerHex(dll);
                string cv = ReadAssemblyVersion(dll) ?? ReadFileVersion(dll);
                string arch = ReadGgmArch(dll);
                if (
                    string.IsNullOrEmpty(hash)
                    || string.IsNullOrEmpty(cv)
                    || string.IsNullOrEmpty(arch)
                )
                    return null;

                Debug.WriteLine($"[GgmIntegrity] local {dll} CV={cv} arch={arch}");
                return new GgmIntegrity(cv, hash, arch);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GgmIntegrity] local DLL failed: {ex.Message}");
                return null;
            }
        }

        private static GgmIntegrity ParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            var obj = JObject.Parse(json);
            string cv = obj["cv"]?.ToString();
            string hash = obj["hash"]?.ToString()?.Trim()?.ToLowerInvariant();
            string arch = obj["arch"]?.ToString();
            if (string.IsNullOrEmpty(cv) || !IsSha256Hex(hash) || (arch != "x64" && arch != "x86"))
                return null;
            return new GgmIntegrity(cv, hash, arch);
        }

        private static bool IsSha256Hex(string hash)
        {
            if (hash == null || hash.Length != 64)
                return false;
            foreach (char c in hash)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }
            return true;
        }

        private static string LocateGgmDll()
        {
            string fromHandler = DirFromProtocolHandler();
            if (fromHandler != null)
            {
                string candidate = Path.Combine(fromHandler, DllName);
                if (File.Exists(candidate))
                    return candidate;
            }

            foreach (
                Environment.SpecialFolder folder in new[]
                {
                    Environment.SpecialFolder.ProgramFiles,
                    Environment.SpecialFolder.ProgramFilesX86,
                }
            )
            {
                string root = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(root))
                    continue;
                string candidate = Path.Combine(
                    root,
                    "gamania Games",
                    "gamania Games Manager",
                    DllName
                );
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        private static string DirFromProtocolHandler()
        {
            try
            {
                using RegistryKey key = Registry.ClassesRoot.OpenSubKey(
                    @"gamaniagames\shell\open\command"
                );
                string command = key?.GetValue("") as string;
                if (string.IsNullOrWhiteSpace(command))
                    return null;
                string exe = HandlerExecutable(command.Trim());
                return string.IsNullOrEmpty(exe) ? null : Path.GetDirectoryName(exe);
            }
            catch
            {
                return null;
            }
        }

        private static string HandlerExecutable(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return null;
            string trimmed = command.Trim();
            if (trimmed.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = trimmed.IndexOf('"', 1);
                if (end <= 1)
                    return null;
                string exe = trimmed.Substring(1, end - 1);
                return exe.Length == 0 ? null : exe;
            }
            int space = trimmed.IndexOfAny(new[] { ' ', '\t' });
            string first = space < 0 ? trimmed : trimmed.Substring(0, space);
            return string.IsNullOrEmpty(first) ? null : first;
        }

        private static string Sha256LowerHex(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using SHA256 sha = SHA256.Create();
            return BitConverter
                .ToString(sha.ComputeHash(bytes))
                .Replace("-", "")
                .ToLowerInvariant();
        }

        private static string ReadAssemblyVersion(string path)
        {
            try
            {
                return AssemblyName.GetAssemblyName(path).Version?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string ReadFileVersion(string path)
        {
            try
            {
                string version = FileVersionInfo.GetVersionInfo(path).FileVersion;
                return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static string ReadGgmArch(string dll)
        {
            string exe = Path.ChangeExtension(dll, ".exe");
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                string exeArch = ReadPeArch(exe);
                if (exeArch != null)
                    return exeArch; // AnyCPU DLL 的 PE 一律是 x86，改讀同目錄 exe
            }
            return ReadPeArch(dll);
        }

        private static string ReadPeArch(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            if (br.ReadUInt16() != 0x5A4D)
                return null;
            fs.Seek(0x3C, SeekOrigin.Begin);
            int peOffset = br.ReadInt32();
            fs.Seek(peOffset, SeekOrigin.Begin);
            if (br.ReadUInt32() != 0x00004550)
                return null;
            ushort machine = br.ReadUInt16();
            if (machine == 0x8664)
                return "x64";
            if (machine == 0x014C)
                return "x86";
            return null;
        }
    }
}
