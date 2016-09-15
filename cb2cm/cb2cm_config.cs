using System;
using System.IO;
using System.Xml.Serialization;

using CamBam;

namespace Cb2cm
{
    public class Cb2cm_config
    {
        public enum Sim_resolutions
        {
            LOW,
            MEDIUM,
            HIGH,
            VERY_HIGH,
        }

        public static Cb2cm_config defaults;

        public Sim_resolutions sim_resolution { get; set; }

        public double default_tool_length{ get; set; }

        public string camotics_path{ get; set; }

        public bool should_launch_camotics{ get; set; }
        
        public bool only_single_instance{ get; set; }

        public bool regen_gfile_before_post{ get; set; }

        public Cb2cm_config()
        {
            sim_resolution = Sim_resolutions.MEDIUM;
            default_tool_length = 10;
            camotics_path = "";
            should_launch_camotics = true;
            only_single_instance = true;
            regen_gfile_before_post = true;
        }

        static string get_config_path()
        {
            return CamBam.FileUtils.GetFullPath(CamBamConfig.Defaults.SystemPath, "cb2cm.config");            
        }

        public static bool save()
        {
            if (defaults == null) return false;

            try
            {
                FileInfo fi = new FileInfo(get_config_path());
                if (!fi.Directory.Exists)                
                    fi.Directory.Create();                

                XmlSerializer serializer = new XmlSerializer(defaults.GetType());

                using (TextWriter writer = new StreamWriter(get_config_path(), false))
                {
                    serializer.Serialize(writer, defaults);
                    writer.Close();
                }
            }
            catch (Exception e)
            {
                ThisApplication.HandleException(e);
                return false;
            }
            return true;
        }

        public static void load()
        {
            Cb2cm_config cfg = null;

            try
            {
                if (File.Exists(get_config_path()))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Cb2cm_config));

                    using (TextReader reader = new StreamReader(get_config_path()))
                    {
                        cfg = (Cb2cm_config)(serializer.Deserialize(reader));

                        reader.Close();
                    }
                }
            }
            catch (Exception e)
            {
                ThisApplication.MsgBox(String.Format("Error reading config file : {0}\r\n{1}", get_config_path(), e.Message));
            }

            if (cfg == null)
                cfg = new Cb2cm_config();

            defaults = cfg;
        }
    }
}
