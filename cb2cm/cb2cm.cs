using System;
using System.IO;
using System.Windows.Forms;


using CamBam;
using CamBam.UI;
using CamBam.CAM;
using CamBam.CAD;

namespace Cb2cm
{
    public class Host
    {
        static public void log(string s, params object[] args)
        {
            ThisApplication.AddLogMessage(s, args);
        }
        static public void warn(string s, params object[] args)
        {
            ThisApplication.AddLogMessage("Warning: " + s, args);
        }
        static public void err(string s, params object[] args)
        {
            ThisApplication.AddLogMessage("Error: " + s, args);
        }
        static public void msg(string s, params object[] args)
        {
            ThisApplication.MsgBox(String.Format(s, args));
        }        
        static public void sleep(int ms)
        {
            System.Threading.Thread.Sleep(ms);
            System.Windows.Forms.Application.DoEvents();
        }
    }

    public class Cb2cm_plugin
    {
        static void tool_popup_handler(object sender, EventArgs e)
        {
            run();
        }

        public static void InitPlugin(CamBamUI ui)
        {
            string config_path = CamBam.FileUtils.GetFullPath(CamBamConfig.Defaults.SystemPath, "cb2cm.config");            

            if (! Cb2cm_config.load(config_path))
                Cb2cm_config.save(config_path);        // Try to create a fresh config


            string cm_path = Cm_launcher.detect_cm(Cb2cm_config.defaults.camotics_path);
            if (Cb2cm_config.defaults.camotics_path != cm_path)
            {
                Cb2cm_config.defaults.camotics_path = cm_path;
                Cb2cm_config.save(config_path);
            }
         
            ToolStripMenuItem tool_popup = new ToolStripMenuItem("Simulate with CAMotics");
            tool_popup.Click += new EventHandler(tool_popup_handler);
            ui.Menus.mnuTools.DropDownItems.Add(tool_popup);
        }

        static void run()
        {
            CADFile doc = CamBamUI.MainUI.CADFileTree.CADFile;
            ICADView view = CamBamUI.MainUI.ActiveView;

            if (doc.Filename == null)
            {
                Host.msg("Please save the project first");
                return;
            }

            if (Cb2cm_config.defaults.regen_gfile_before_post)
            {
                CAMUtils.GenerateGCodeOutput(view);
                do
                {
                    Host.sleep(1);
                } while (view.IsThinking);
            }

            Cm_xml_generator gen = new Cm_xml_generator(doc);
            string cm_xml = gen.run();
            string cm_xml_path = Path.ChangeExtension(gen.g_fullname, "xml");

            System.IO.StreamWriter file = new System.IO.StreamWriter(cm_xml_path);
            file.Write(cm_xml);
            file.Close();
            Host.log("Created CAMotics project {0}", cm_xml_path);

            if (!Cb2cm_config.defaults.should_launch_camotics)
                return;

            Cm_launcher.run(Cb2cm_config.defaults.camotics_path, cm_xml_path, Cb2cm_config.defaults.only_single_instance);
        }        
    }

}
