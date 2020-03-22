using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ePSXeArchiveLauncher
{
    class Program
    {
        private readonly static String[] extensions = { ".m3u", ".cue", ".ccd", ".mds", ".bin", ".iso", ".mdf", ".img", ".pbp" };
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: ePSXeArchiveLauncher <epsxePath> <archivePath>");
                return;
            }
            String epsxePath = args[0];
            String archivePath = args[1];
            if (!File.Exists(epsxePath) || Path.GetFileNameWithoutExtension(epsxePath).ToLower() != "epsxe") 
            {
                Console.WriteLine("epsxePath is invalid");
                return;
            }

            if (!File.Exists(archivePath))
            {
                Console.WriteLine("archivePath is invalid");
                return;
            }
            
            WADrive drive = new WADrive();
            String mountPoint = drive.MountFile(archivePath);
            String isoPath = findBestMatch(mountPoint, extensions);
            Console.Write($"EPSXE: {epsxePath}\nArchive: {archivePath}\nImage: {isoPath}\n");
            Console.WriteLine("Starting emulator...");
            launchEpsxeAsync(epsxePath, isoPath, mountPoint);
            Console.WriteLine("Unmounting image");
            drive.UnMountDrive(mountPoint);
        }

        private static void launchEpsxeAsync(String epsxe, String iso, String drive=null)
        {
            Directory.SetCurrentDirectory(Directory.GetParent(epsxe).FullName);
            using(var process = new Process())
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(escapePath(epsxe), "-nogui -loadbin " + escapePath(iso));
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;
                process.Start();
                Console.WriteLine("Waiting for emu to close");
                process.WaitForExit();
            }
        }

        private static String escapePath(String path)
        {
            if (path.Contains(" "))
            {
                return "\"" + path + "\"";
            }
            return path;
        }

        private static String findBestMatch(String mountPoint, params String[] ext)
        {
            List<String> files = searchISO(mountPoint, ext);
            foreach (var e in ext)
            {
                foreach (var f in files)
                {
                    if (Path.GetExtension(f).ToLower().Equals(e))
                    {
                        return f;
                    }
                }
            }
            throw new FileNotFoundException("Game image not found");
        }

        private static List<String> searchISO(String mountPoint, params String[] ext)
        {
            if (mountPoint == null)
            {
                return new List<string>();
            }
            List<String> matches = new List<String>();
            string[] files = Directory.GetFileSystemEntries(Path.GetFullPath(mountPoint));
            foreach (var f in files)
            {
                String currExt = Path.GetExtension(f).ToLower();
                if (ext.Any(e => e.Equals(currExt)))
                {
                    matches.Add(f);
                }
            }

            foreach (var directory in Directory.GetDirectories(mountPoint))
            {
                var isos = searchISO(directory, ext);
                if (isos != null && isos.Count > 0)
                {
                    matches.AddRange(isos);
                }
            }
            return matches;
        }
    }
}
