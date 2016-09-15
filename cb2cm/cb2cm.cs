using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;


using CamBam;
using CamBam.UI;
using CamBam.CAM;
using CamBam.CAD;

namespace Cb2cm
{
    public class Cb2cm_plugin
    {
        static void tool_popup_handler(object sender, EventArgs e)
        {
            run();
        }

        public static void InitPlugin(CamBamUI ui)
        {
            Cb2cm_config.load();

            string cm_path = Cm_launcher.detect_cm(Cb2cm_config.defaults.camotics_path);
            if (Cb2cm_config.defaults.camotics_path != cm_path)
            {
                Cb2cm_config.defaults.camotics_path = cm_path;
                Cb2cm_config.save();
            }
         
            ToolStripMenuItem tool_popup = new ToolStripMenuItem("Simulate with CAMotics");
            tool_popup.Click += new EventHandler(tool_popup_handler);
            ui.Menus.mnuTools.DropDownItems.Add(tool_popup);

            /*
                ToolStripMenuItem mop_popup = new ToolStripMenuItem("Simulate with CAMotics");
                mop_popup.Click += mop_popup_handler;

                ToolStripMenuItem part_popup = new ToolStripMenuItem("Simulate with CAMotics");
                part_popup.Click += part_popup_handler;

                ToolStripMenuItem machining_popup = new ToolStripMenuItem("Simulate with CAMotics");
                machining_popup.Click += machining_popup_handler;

                ui.ViewContextMenus.MachiningContextMenu.Items.Insert(2, machining_popup);
                ui.ViewContextMenus.MOPContextMenu.Items.Insert(2, mop_popup);
                ui.ViewContextMenus.PartContextMenu.Items.Insert(2, part_popup);
            */
        }

        /*
        static void mop_popup_handler(object sender, EventArgs e)
        {
            List<MachineOp> selected_mops = CamBamUI.MainUI.ActiveView.DrawingTree.SelectedMOPs;
            //MachineOp active_mop = active_part.;
            string filename = CAMUtils.GetGCodeFilename(selected_mops[0]);
            ThisApplication.AddLogMessage("MOP {0} {1}", selected_mops[0].Name, filename);
        }

        static void part_popup_handler(object sender, EventArgs e)
        {
            List<CAMPart> selected_parts = CamBamUI.MainUI.ActiveView.DrawingTree.SelectedParts;
            string filename = CAMUtils.GetGCodeFilename(selected_parts[0]);

            ThisApplication.AddLogMessage("PART {0} {1}", selected_parts[0].Name, filename);
        }

        static void machining_popup_handler(object sender, EventArgs e)
        {
            ThisApplication.AddLogMessage("MACH");
        }
        */

        static void run()
        {
            CADFile doc = CamBamUI.MainUI.CADFileTree.CADFile;
            ICADView view = CamBamUI.MainUI.ActiveView;

            if (doc.Filename == null)
            {
                ThisApplication.MsgBox("Please save the project first");
                return;
            }

            if (Cb2cm_config.defaults.regen_gfile_before_post)
            {
                CAMUtils.GenerateGCodeOutput(view);
                do
                {
                    System.Threading.Thread.Sleep(1);
                    System.Windows.Forms.Application.DoEvents();
                } while (view.IsThinking);
            }

            Cm_xml_generator gen = new Cm_xml_generator(doc);
            string cm_xml = gen.run();
            string cm_xml_path = Path.ChangeExtension(gen.g_name, "xml");

            System.IO.StreamWriter file = new System.IO.StreamWriter(cm_xml_path);
            file.Write(cm_xml);
            file.Close();
            ThisApplication.AddLogMessage("Created CAMotics project {0}", cm_xml_path);

            if (!Cb2cm_config.defaults.should_launch_camotics)
                return;

            Cm_launcher.run(Cb2cm_config.defaults.camotics_path, cm_xml_path, Cb2cm_config.defaults.only_single_instance);
        }        
    }

}
