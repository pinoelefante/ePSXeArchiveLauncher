using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ePSXeArchiveLauncher
{
    public class WADrive
    {
        private readonly string WIN_ARCHIVER_CMD = "wacmd.exe";
        private String winCmdPath;
        public WADrive()
        {
            winCmdPath = SearchWinArchiver();
        }

        private String SearchWinArchiver()
        {
            try
            {
                return SearchFromRegistry();
                
            } catch
            {
                return SearchProgramDir();
            }
        }

        private String SearchFromRegistry()
        {
            String keyName32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\WinArchiver";
            String keyName64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinArchiver";
            String path64 = Registry.GetValue(keyName64, "Install_Dir_x64", "").ToString();
            String path32 = Registry.GetValue(keyName32, "Install_Dir_x86", "").ToString();
            String[] paths = new String[] { path64, path32};
            if (!checkPaths(paths))
                throw new FileNotFoundException("WinArchiver registry key not found");
            String path = paths.FirstOrDefault(p => !string.IsNullOrEmpty(p) && Directory.Exists(p) && File.Exists(Path.Combine(p, WIN_ARCHIVER_CMD)));
            return Path.Combine(path, WIN_ARCHIVER_CMD);
        }

        private bool checkPaths(String[] paths)
        {
            if (paths == null)
                return false;
            return paths.Any(p => !string.IsNullOrEmpty(p) && Directory.Exists(p) && File.Exists(Path.Combine(p, WIN_ARCHIVER_CMD)));
        }

        private String SearchProgramDir()
        {
            String dir64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinArchiver");
            String dir32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinArchiver");
            if (Directory.Exists(dir64) && File.Exists(Path.Combine(dir64, WIN_ARCHIVER_CMD)))
                return Path.Combine(dir64, WIN_ARCHIVER_CMD);
            if (Directory.Exists(dir32) && File.Exists(Path.Combine(dir32, WIN_ARCHIVER_CMD)))
                return Path.Combine(dir32, WIN_ARCHIVER_CMD);
            throw new FileNotFoundException("WinArchiver not found in program dir");
        }

        public List<Tuple<String, bool>> ListDrives()
        {
            return CallProcess("listvd").Select(line =>
            {
                String drive = line.Substring(line.IndexOf('[') + 1, 2);
                bool isEmpty = line.Contains("<No media>");
                return new Tuple<String, bool>(drive, isEmpty);
            }).ToList();
        }

        public String GetFirstEmptyDrive(List<Tuple<String, bool>> drives)
        {
            if (drives.Any(d => d.Item2 == true))
            {
                return drives.First(d => d.Item2 == true).Item1;
            }
            CreateNewDriver();
            return GetFirstEmptyDrive(ListDrives());
        }

        public void CreateNewDriver()
        {
            int drives = ListDrives().Count;
            if (drives >= 8)
            {
                throw new InvalidOperationException("Drive limit is 8. UnMount a drive manually");
            }
            if (CallProcess("setvdnum " + (drives + 1)).Count > 0)
            {
                throw new InvalidOperationException("There was a problem creating a new drive");
            }
        }

        public String MountFile(String archivePath, String drive=null)
        {
            if (drive == null)
            {
                var drives = ListDrives();
                String driveLetter = GetFirstEmptyDrive(drives);
                return MountFile(archivePath, driveLetter);
            } 
            else
            {
                List<String> res = CallProcess($"mount \"{archivePath}\" {drive}");
                if (res.ElementAt(0).Contains("fail"))
                    throw new InvalidOperationException("File not mounted");
            }
            return drive;
        }

        public bool UnMountDrive(String drive=null)
        {
            return CallProcess("unmount " + (drive == null ? "all" : drive)).ElementAt(0).Contains("Unmount successfully");
        }

        private List<String> CallProcess(String parameters)
        {
            List<String> output = new List<String>();
            using(var process = new Process())
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(winCmdPath, parameters);
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;
                process.StartInfo = startInfo;
                process.OutputDataReceived += (sender, data) =>
                {
                    if (!String.IsNullOrEmpty(data.Data?.Trim()))
                    {
                        output.Add(data.Data.Trim());
                    }
                };

                process.EnableRaisingEvents = true;
                if (process.Start())
                {
                    process.BeginOutputReadLine();
                    process.WaitForExit();
                }
            }
            return output.Skip(2).ToList();
        }
    }
}
