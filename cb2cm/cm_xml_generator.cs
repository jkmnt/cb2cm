using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;

using CamBam;
using CamBam.Geom;
using CamBam.CAD;
using CamBam.CAM;
using CamBam.Library;

namespace Cb2cm
{
    class Cm_xml_generator
    {
        class Stock
        {
            public Point3F min;
            public Point3F max;
        }

        class Tool
        {
            public double diameter;
            public string desc;
            public int index;
            public double length;
            public bool are_units_imperial;
            public string shape;
        };

        CADFile doc;        

        public Cm_xml_generator(CADFile cadfile)
        {
            doc = cadfile;            
        }

        double mm_scale
        {
            get
            {
                Units units = doc.DrawingUnits;
                if (units == Units.Inches) return 25.4;
                if (units == Units.Centimeters) return 10.0;
                if (units == Units.Meters) return 1000.0;
                if (units == Units.Thousandths) return 0.0254;
                return 1.0;
            }
        }

        bool is_cad_imperial
        {
            get
            {                
                return doc.DrawingUnits == Units.Inches || doc.DrawingUnits == Units.Thousandths;
            }
        }

        Point3F unpack_p3f(Point3F input)
        {            
            return new Point3F(input.X * mm_scale, input.Y * mm_scale, input.Z * mm_scale);            
        }

        Stock get_stock(StockDef stock)
        {
            Stock output = new Stock();
            output.min = unpack_p3f(stock.PMin);
            output.max = unpack_p3f(stock.PMax);
            return output;
        }

        bool are_tool_units_imperial(ToolDefinition tool)
        {
            string name = tool.ToolLibrary.Name;
            if (name.EndsWith("-mm")) return false;
            if (name.EndsWith("-in")) return true;
            return is_cad_imperial;
        }

        string map_tool_shape(ToolDefinition tool)
        {
            if (tool.ToolProfile == ToolProfiles.VCutter) return "CONICAL";
            if (tool.ToolProfile == ToolProfiles.BallNose || tool.ToolProfile == ToolProfiles.BullNose) return "BALLNOSE";
            return "CYLINDRICAL";
        }

        Tool get_tool_from_mop(MachineOp mop)
        {
            ToolDefinition tool = mop.CurrentTool;

            if (tool == null)
                return null;            

            Tool output = new Tool();

            output.desc = tool.DisplayName;
            output.index = tool.Index;
            output.are_units_imperial = are_tool_units_imperial(tool);
            output.shape = map_tool_shape(tool);

            if (tool.ToolProfile != ToolProfiles.VCutter)
            {
                output.diameter= tool.Diameter;
                output.length = tool.FluteLength != 0 ? tool.FluteLength : Cb2cm_config.defaults.default_tool_length;
            }
            else
            {
                output.diameter = tool.ShankDiameter;
                // supply a common 1/8" shank for the undefined                
                if (output.diameter <= 0)
                    output.diameter = output.are_units_imperial ? 0.125 : 3.175;

                double angle = tool.VeeAngle;

                if (tool.VeeAngle <= 0)
                {
                    angle = 45.0;
                    Logger.warn("Conical tool {0} has no Vee angle specified. Defaulting to the 45 degrees", tool.Index);
                }
                output.length = output.diameter / 2 / Math.Tan(Math.PI * angle / 2 / 180);                
            }                       
            
            return output;
        }

        public string g_fullname
        {
            get
            {
                return FileUtils.GetFullPath(doc.MachiningOptions.CADFile, doc.MachiningOptions.OutFile);
            }
        }

        public string g_name
        {
            get
            {
                return Path.GetFileName(g_fullname);
            }
        }

        XElement gen_cm_project(Stock stock, Dictionary<int, Tool> tools)
        {            
            XElement root = new XElement("camotics");
            root.Add(new XElement("nc-files", g_name.Replace(" ", "%20")));
            root.Add(new XElement("resolution-mode", new XAttribute("v", Cb2cm_config.defaults.sim_resolution)));
            root.Add(new XElement("units", new XAttribute("v", is_cad_imperial ? "INCH" : "MM")));
            if (stock == null)
            {
                root.Add(new XElement("automatic-workpiece", new XAttribute("v", "true")));
            }
            else
            {
                root.Add(new XElement("automatic-workpiece", new XAttribute("v", "false")));
                root.Add(new XElement("workpiece-max", new XAttribute("v", "(" + stock.max.ToString() + ")")),
                         new XElement("workpiece-min", new XAttribute("v", "(" + stock.min.ToString() + ")")));
            }

            XElement tt = new XElement("tool_table");
            root.Add(tt);

            foreach (var pair in tools)
            {
                Tool tool = pair.Value;
                tt.Add(new XElement("tool",
                                    new XAttribute("length", tool.length),
                                    new XAttribute("number", tool.index),
                                    new XAttribute("radius", tool.diameter / 2),
                                    new XAttribute("shape", tool.shape),
                                    new XAttribute("units", tool.are_units_imperial ? "INCH" : "MM"),
                                    tool.desc
                                   ));
            }

            return root;
        }

        public string run()
        {                    
            MachiningOptions mopts = doc.MachiningOptions;
            CAMParts parts = doc.Parts;

            List<Stock> stocks = new List<Stock>();

            foreach (CAMPart part in parts)
            {
                if (!part.Stock.IsUndefined)
                    stocks.Add(get_stock(part.Stock));
            }

            if (!mopts.Stock.IsUndefined)
                stocks.Add(get_stock(mopts.Stock));

            Stock stock = stocks.Count > 0 ? stocks[0] : null;
            
            Dictionary<int, Tool> tools = new Dictionary<int, Tool>();

            foreach (CAMPart part in parts)
            {
                if ((!part.Enabled) && Cb2cm_config.defaults.skip_disabled_mops)
                    continue;

                foreach (MachineOp mop in part.MachineOps)
                {
                    if (!(mop is MOPFromGeometry))
                        continue;

                    if ((!mop.Enabled) && Cb2cm_config.defaults.skip_disabled_mops)
                        continue;

                    Tool tool = get_tool_from_mop(mop);                    
                    if (tool == null)
                    {
                        Logger.warn("{0}/{1} has no library tool defined. Sim results may be wrong", part.Name, mop.Name);
                    }
                    else
                    {
                        // warn if tool diameter is overriden in mop
                        // XXX: floating point compare may be inaccurate, but in this case value should be a copy, not a calculated one.
                        if ((double)mop.ToolDiameter.GetValue() != tool.diameter)
                        {
                            // XXX: diameter for conicals is a cut width, not an actual diameter. no need to rise the alarm.
                            if (mop.CurrentTool.ToolProfile != ToolProfiles.VCutter)
                                Logger.warn("{0}/{1} has tool diameter overridden. Sim results may be wrong", part.Name, mop.Name);
                        }
                        // NOTE: tool will overwrite previous tool with same index
                        tools[tool.index] = tool;
                    }
                }
            }

            return gen_cm_project(stock, tools).ToString();            
        }
    }
}
