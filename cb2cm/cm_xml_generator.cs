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

        struct Tool
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

        bool is_toollib_imperial(string libname)
        {
            if (libname != null)
            {
                if (libname.EndsWith("-mm")) return false;
                if (libname.EndsWith("-in")) return true;
            }
            return is_cad_imperial;
        }

        string map_tool_shape(ToolProfiles profile)
        {
            if (profile == ToolProfiles.VCutter) return "CONICAL";
            if (profile == ToolProfiles.BallNose || profile == ToolProfiles.BullNose) return "BALLNOSE";
            return "CYLINDRICAL";
        }

        Tool get_library_tool(ToolDefinition tool)
        {
            Tool output = new Tool();

            output.desc = tool.DisplayName;
            output.index = tool.Index;
            output.are_units_imperial = is_toollib_imperial(tool.ToolLibrary.Name);
            output.shape = map_tool_shape(tool.ToolProfile);
            output.diameter = tool.Diameter;

            if (tool.ToolProfile != ToolProfiles.VCutter)
            {
                output.length = tool.FluteLength != 0 ? tool.FluteLength : Cb2cm_config.defaults.default_tool_length;
            }
            else
            {
                double angle = tool.VeeAngle;

                if (tool.VeeAngle <= 0)
                {
                    angle = 45.0;
                    Host.warn("Conical tool {0} has no Vee angle specified. Defaulting to the 45 degrees", tool.Index);
                }
                output.length = output.diameter / 2 / Math.Tan(Math.PI * angle / 2 / 180);
            }

            return output;
        }

        Tool get_mop_tool(MachineOp mop)
        {
            Tool tool = new Tool();
            tool.index = mop.ToolNumber.Cached;
            tool.diameter = mop.ToolDiameter.Cached;
            tool.length = Cb2cm_config.defaults.default_tool_length;
            tool.shape = map_tool_shape(mop.ToolProfile.Cached);
            tool.are_units_imperial = is_toollib_imperial(mop.Part.ToolLibrary);
            tool.desc = String.Format("MOP-specified tool {0}{1}", tool.diameter, tool.are_units_imperial ? "in" : "mm");
            return tool;
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

                    ToolDefinition libtool = null;

                    if (mop.ToolNumber.Cached == 0)
                    {
                        Host.warn("{0}/{1} has no tool number defined. Sim results may be wrong", part.Name, mop.Name);
                    }
                    else
                    {
                        libtool = mop.CurrentTool;
                    }


                    Tool tool;

                    if (libtool == null || mop.ToolDiameter.Cached != libtool.Diameter|| mop.ToolProfile.Cached != libtool.ToolProfile)
                    {
                        tool = get_mop_tool(mop);
                    }
                    else
                    {
                        tool = get_library_tool(libtool);
                    }

                    if (tool.diameter == 0)
                        Host.warn("{0}/{1} has zero diameter tool {2})", part.Name, mop.Name, mop.ToolNumber);

                    if (tools.ContainsKey(tool.index) && (! tool.Equals(tools[tool.index])))
                    {
                        Host.warn("{0}/{1} has conflicting specification for tool {2}", part.Name, mop.Name, tool.index);
                    }
                    else
                    {
                        tools[tool.index] = tool;
                    }
                }
            }

            return gen_cm_project(stock, tools).ToString();
        }
    }
}
