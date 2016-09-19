using System;
using System.IO;
using System.Xml.Serialization;

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

        public bool skip_disabled_mops { get; set; }

        public Cb2cm_config()
        {            
            sim_resolution = Sim_resolutions.MEDIUM;
            default_tool_length = 10;
            camotics_path = "";
            should_launch_camotics = true;
            only_single_instance = true;
            regen_gfile_before_post = true;
            skip_disabled_mops = true;
        }

        public static bool save(string path)
        {
            if (defaults == null) return false;

            try
            {
                FileInfo fi = new FileInfo(path);
                if (!fi.Directory.Exists)                
                    fi.Directory.Create();                

                XmlSerializer serializer = new XmlSerializer(defaults.GetType());

                using (TextWriter writer = new StreamWriter(path, false))
                {
                    serializer.Serialize(writer, defaults);
                    writer.Close();
                }
            }
            catch (Exception e)
            {
                Logger.err("Failed to save config file : {0}\r\n{1}", path, e.Message);                
                return false;
            }
            return true;
        }

        public static bool load(string path)
        {
            Cb2cm_config cfg = null;

            try
            {
                if (File.Exists(path))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Cb2cm_config));

                    using (TextReader reader = new StreamReader(path))
                    {
                        cfg = (Cb2cm_config)(serializer.Deserialize(reader));

                        reader.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.err("Failed to read config file : {0}\r\n{1}", path, e.Message);
            }

            if (cfg == null)
            {
                defaults = new Cb2cm_config();
                return false;
            }
            
            defaults = cfg;
            return true;
        }
    }
}
