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
            public double radius;
            public string desc;
            public int index;
            public double length;
            public string units;
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

        string guess_tool_units(ToolDefinition tool)
        {
            string name = tool.ToolLibrary.Name;
            if (name.EndsWith("-mm")) return "MM";
            if (name.EndsWith("-in")) return "INCH";
            return is_cad_imperial ? "INCH" : "MM";
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

            output.radius = tool.Diameter / 2;
            output.desc = tool.DisplayName;
            output.index = tool.Index;
            output.length = tool.FluteLength != 0 ? tool.FluteLength : Cb2cm_config.defaults.default_tool_length;
            output.units = guess_tool_units(tool);
            output.shape = map_tool_shape(tool);
            return output;
        }

        public string g_name
        {
            get
            { 
                return FileUtils.GetFullPath(doc.MachiningOptions.CADFile, doc.MachiningOptions.OutFile);
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
                                    new XAttribute("radius", tool.radius),
                                    new XAttribute("shape", tool.shape),
                                    new XAttribute("units", tool.units)
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
                foreach (MachineOp mop in part.MachineOps)
                {
                    if (!(mop is MOPFromGeometry))
                        continue;
                    Tool tool = get_tool_from_mop(mop);
                    // NOTE: tool will overwrite previous tool with same index
                    if (tool != null)
                        tools[tool.index] = tool;
                }
            }

            return gen_cm_project(stock, tools).ToString();            
        }
    }
}
