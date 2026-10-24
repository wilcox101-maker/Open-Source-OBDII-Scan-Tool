using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using ObdLink.Core.Firmware;

namespace ObdLink.App;

/// <summary>v0.3.1 Windows-only helpers for the official updater: Authenticode (WinVerifyTrust) and install detection (registry + Program Files).</summary>
internal static class UpdaterWindows
{
    public static SignatureInfo VerifySignature(string path)
    {
        bool has;
        try { has = OfficialUpdater.HasAuthenticodeBlob(File.ReadAllBytes(path)); } catch { return new(false, false, null); }
        if (!has) return new(false, false, null);
        string? pub = null;
#pragma warning disable SYSLIB0057
        try { pub = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)).GetNameInfo(X509NameType.SimpleName, false); } catch { }
#pragma warning restore SYSLIB0057
        return new(true, WinTrust(path) == 0, pub);
    }

    public static string? FindInstalled()
    {
        var entries = new List<(string?, string?, string?)>();
        void Scan(RegistryKey root, string key)
        {
            try
            {
                using var k = root.OpenSubKey(key); if (k is null) return;
                foreach (var n in k.GetSubKeyNames())
                {
                    using var s = k.OpenSubKey(n);
                    if (s is not null) entries.Add((s.GetValue("DisplayName") as string, s.GetValue("InstallLocation") as string, s.GetValue("DisplayIcon") as string));
                }
            }
            catch { }
        }
        Scan(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        Scan(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        Scan(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        var pf = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        };
        return OfficialUpdater.FindInstalled(entries, pf, File.Exists);
    }

    // ---- WinVerifyTrust (WINTRUST_ACTION_GENERIC_VERIFY_V2), whole chain, no UI, online revocation off ----
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfoW { public int cbStruct; public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public int cbStruct; public IntPtr pPolicyCallbackData, pSIPClientData; public int dwUIChoice, fdwRevocationChecks, dwUnionChoice;
        public IntPtr pFile; public int dwStateAction; public IntPtr hWVTStateData, pwszURLReference; public int dwProvFlags, dwUIContext; public IntPtr pSignatureSettings;
    }
    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref TrustData data);
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private static int WinTrust(string path)
    {
        var fi = new FileInfoW { cbStruct = Marshal.SizeOf<FileInfoW>(), pcwszFilePath = path };
        var pFi = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoW>());
        try
        {
            Marshal.StructureToPtr(fi, pFi, false);
            var d = new TrustData { cbStruct = Marshal.SizeOf<TrustData>(), dwUIChoice = 2 /*NONE*/, fdwRevocationChecks = 0, dwUnionChoice = 1 /*FILE*/, pFile = pFi, dwStateAction = 1 /*VERIFY*/, dwProvFlags = 0x80 /*REVOCATION_CHECK_NONE*/ };
            int r = WinVerifyTrust(new IntPtr(-1), GenericVerifyV2, ref d);
            d.dwStateAction = 2; /*CLOSE*/ _ = WinVerifyTrust(new IntPtr(-1), GenericVerifyV2, ref d);
            return r;
        }
        catch { return -1; }
        finally { Marshal.DestroyStructure<FileInfoW>(pFi); Marshal.FreeHGlobal(pFi); }
    }
}
