using System;
using System.Diagnostics;

using CamBam;

namespace Cb2cm
{
    class Cm_launcher
    {
        static public string detect_cm(string default_path)
        {
            // try a passed path and some common locations
            string[] locations =
            {
                default_path,
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "/CAMotics/camotics.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.Programs) + "/CAMotics/camotics.exe",
                "c:/Program Files (x86)/CAMotics/camotics.exe",
                "c:/Program Files/CAMotics/camotics.exe"
            };
            foreach (string path in locations)
            {
                if (System.IO.File.Exists(path))
                    return path;
            }
            // try to obtain path from a possibly running app
            Process[] processes = Process.GetProcessesByName("camotics");
            if (processes.Length > 0)
                return processes[0].MainModule.FileName;
            return null;
        }

        static void close()
        {
            Process[] processes = Process.GetProcessesByName("camotics");
            foreach (Process proc in processes)
            {
                proc.CloseMainWindow();
                proc.WaitForExit(1000);

                if (!proc.HasExited)
                {
                    ThisApplication.AddLogMessage("Waiting for CAMotics to exit");
                    do
                    {
                        System.Threading.Thread.Sleep(1);
                        System.Windows.Forms.Application.DoEvents();
                    } while (!proc.HasExited);
                }
            }
        }

        static public void run(string path, string project, bool limit_to_single_instance)
        {
            if (limit_to_single_instance)
                close();
            try
            {
                Process.Start(path, project);
            }
            catch (Exception e)
            {
                ThisApplication.MsgBox(String.Format("Failed to run CAMotics. Set location in cb2cm.config file or start CAMotics manually before CamBam to autodetect it. \r\n\n{0}", e.Message));
            }
        }
    }
}