// TO-DO LIST:
// - Combine the two files and zip it up as gz
// - Anonymous error collection: implement a data file as part of solution to store yes/no, # of successful runs, email ID?
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;

namespace vERAM_Geo_Map
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string facility;

                PrintHeader();

                facility = ImportConfig();
                QueryRadar(facility);

                MakeMapSet(facility);

                ZipFacilityConfig(facility);

                Console.Write("Press any key to exit...");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                SmtpClient mail = new SmtpClient("smtp.gmail.com", 587);
                mail.EnableSsl = true;
                mail.UseDefaultCredentials = false;
                MailAddress from = new MailAddress("vzoasoftware@gmail.com", "AppCrash");
                MailAddress to = new MailAddress("fe@oakartcc.org", "Srinath Nandakumar");
                MailMessage message = new MailMessage(from, to);

                message.Subject = "vERAM Facility Maker Crash";
                message.Body = e.ToString();

                NetworkCredential creds = new NetworkCredential("vzoasoftware@gmail.com", "radar45ts");
                mail.Credentials = creds;

                mail.Send(message);

                Console.Write("Something caused the app to crash. Press any key to exit");
                Console.ReadLine();
            }
            
        }

        static void ZipFacilityConfig(string facility)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;

            XmlWriterSettings wsettings = new XmlWriterSettings();
            wsettings.Indent = true;
            wsettings.IndentChars = ("\t");
            wsettings.OmitXmlDeclaration = false;

            XmlWriter writer = XmlWriter.Create(facility + " Facility Configuration.xml", wsettings);
            writer.WriteStartDocument();
            writer.WriteStartElement("ERAMFacilityBundle");
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");


            using (XmlReader reader = XmlReader.Create(facility + "_Config.xml", settings))
            {
                reader.ReadToFollowing("Facility");
                while (!reader.EOF)
                {
                    writer.WriteNode(reader, true);
                }
            }

            using (XmlReader reader = XmlReader.Create(facility + "_GeoMaps.xml", settings))
            {
                writer.WriteStartElement("GeoMapSet");
                writer.WriteAttributeString("DefaultMap", "CERTMAP");
                reader.ReadToFollowing("BcgMenus");

                while (!reader.EOF)
                {
                    writer.WriteNode(reader, true);
                }
            }

            writer.WriteEndElement();           // </ERAMFacilityBundle>
            writer.WriteEndDocument();
            writer.Close();

            FileInfo fileInfo = new FileInfo(facility + " Facility Configuration.xml");
            DateTime now = DateTime.Now;
            string name = facility + now.ToString("yyyyMMmmss") + ".gz";
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\vERAM Facilities\\" + facility + "\\" + name;

            using (FileStream xml = fileInfo.OpenRead())
            {
                
                using (FileStream compressedFileStream = File.Create(name))
                {
                    
                    using (GZipStream compressionStream = new GZipStream(compressedFileStream,
                       CompressionMode.Compress))
                    {
                        xml.CopyTo(compressionStream);
                    }
                }
            }

            new FileInfo(path).Directory.Create();
            File.Move(name, path);
            Console.WriteLine();
            Console.WriteLine("Facility config successfully created and placed: " + path);
        }

        /// <summary>
        /// Deals with reading and writing information pertaining to radar sites
        /// </summary>

        static void QueryRadar(string facility)
        {
            string response = "a";

            while (response != "y" && response != "n")
            {
                Console.Write("Import Radar Site Data? (y/n): ");
                response = Console.ReadLine().ToLower();
            }

            if (response == "y")
            {
                Console.WriteLine("Confirm a Real-World file named Radar.xml is present in the installation folder?");
                Console.Write("Press any key to continue when the file is present.");
                Console.ReadLine();

                XmlWriterSettings wsettings = new XmlWriterSettings();
                wsettings.Indent = true;
                wsettings.IndentChars = ("\t");
                wsettings.OmitXmlDeclaration = false;

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.IgnoreWhitespace = true;
                settings.IgnoreComments = true;

                XmlWriter writer = XmlWriter.Create(facility + "Temp.xml", wsettings);
                writer.WriteStartDocument();
                writer.WriteStartElement("Facility");

                using (XmlReader reader = XmlReader.Create(facility + "_Config.xml", settings))
                {
                    EditRadars(reader, writer);
                }

                writer.Close();

                System.IO.File.Delete(facility + "_Config.xml");
                System.IO.File.Move(facility + "Temp.xml", facility + "_Config.xml");
                Console.WriteLine("Radar sites successfully imported from file.");
            }

            Console.WriteLine();
        }

        static void EditRadars(XmlReader reader, XmlWriter writer)
        {

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;
            XmlReader radarReader = XmlReader.Create("Radar.xml", settings);
            reader.ReadToFollowing("ID");
            while (reader.Name != "RadarSites")
            {
                writer.WriteNode(reader, true);
            }

            writer.WriteStartElement("RadarSites");

            while (radarReader.ReadToFollowing("RadarParameters"))
            {
                string name;
                string elevation;
                string primaryRange;
                string secondaryRange;
                string lat;
                string lon;

                XmlReader inner = radarReader.ReadSubtree();

                inner.ReadToFollowing("RadarName");
                name = inner.ReadElementContentAsString().Trim();
                Console.WriteLine("Importing Radar Site '" + name + "'");
                inner.ReadToFollowing("PSRMaxRange");
                primaryRange = inner.ReadElementContentAsString().Trim();
                secondaryRange = inner.ReadElementContentAsString().Trim();
                inner.ReadToFollowing("RadarLatitude");
                lat = inner.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });
                lon = inner.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });
                inner.ReadToFollowing("RadarHeight");
                elevation = inner.ReadElementContentAsString().Trim();

                WriteRadarSiteNode(name, elevation, (int.Parse(primaryRange) / 64).ToString(), (int.Parse(secondaryRange) / 64).ToString(), ConvertCoordinate(int.Parse(lat)).ToString(), ConvertCoordinate(-1 * int.Parse(lon)).ToString(), writer);
                inner.Close();
            }
            radarReader.Close();
            writer.WriteEndElement();           // </RadarSites>
            reader.Skip();

            while (reader.Name != "Local")
            {
                writer.WriteNode(reader, true);
            }

            WriteLocalNode(writer);
            reader.Skip();

            while (!reader.EOF)
            {
                writer.WriteNode(reader, true);
            }
        }
        
        /// <summary>
        /// Deals with importing the vERAM settings for the chosen facility
        /// </summary>
        static string ImportConfig()
        {
            string facility;

            Console.Write("Enter the identifier of the desired facility: ");
            facility = Console.ReadLine().ToUpper();


            XmlWriterSettings wsettings = new XmlWriterSettings();
            wsettings.Indent = true;
            wsettings.IndentChars = ("\t");
            wsettings.OmitXmlDeclaration = false;

            XmlWriter writer = XmlWriter.Create(facility + "_Config.xml", wsettings);
            writer.WriteStartDocument();
            writer.WriteStartElement("Facility");

            writer.WriteStartElement("ID");
            writer.WriteString(facility);
            writer.WriteEndElement();

            WriteConfig(facility, writer);
            writer.WriteEndElement();

            writer.Close();

            Console.WriteLine("Present Configuration for " + facility + " Imported");
            Console.WriteLine();

            return facility;
        }

        static void WriteConfig(string facility, XmlWriter facilityWriter)
        {

            string userName = Environment.UserName;
            string path = "C:\\Users\\" + userName + "\\AppData\\Local\\vERAM\\vERAMConfig.xml";

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;
            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                bool flag = false;
                while (reader.ReadToFollowing("ERAMFacility") && !flag)
                {
                    XmlReader inner = reader.ReadSubtree();
                    inner.ReadToDescendant("ID");
                    if (inner.ReadElementContentAsString() == facility)
                    {
                        flag = true;

                        while (inner.NodeType == XmlNodeType.Element)
                        {
                            facilityWriter.WriteNode(inner, true);
                        }
                    }
                    inner.Close();
                }
                if (!flag)
                {
                    reader.Close();
                    facilityWriter.WriteEndElement();
                    facilityWriter.Close();

                    Console.WriteLine("Could not find specified facility settings");
                    Console.Write("Press any key to end...");
                    Console.ReadLine();

                    Environment.Exit(0);
                }
            }

        }

        /// <summary>
        /// Deals with the creation of GeoMaps from real-world data
        /// </summary>
        static void MakeMapSet(string facility)
        {
            Console.WriteLine("Confirm that Real-World files \"GeoMaps.xml\" and \"ConsoleCommandControl.xml\" are present in the installation folder.");
            Console.Write("Press any key to continue.");
            Console.ReadLine();

            bool flag = false;

            XmlWriterSettings wsettings = new XmlWriterSettings();
            wsettings.Indent = true;
            wsettings.IndentChars = ("\t");
            wsettings.OmitXmlDeclaration = false;

            XmlWriter writer = XmlWriter.Create(facility + "_GeoMaps.xml", wsettings);
            writer.WriteStartDocument();
            WriteGeoSetNode(writer);

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;

            using (XmlReader reader = XmlReader.Create("ConsoleCommandControl.xml", settings))
            {
                reader.ReadToFollowing("ConsoleCommandControl_Records");
                writer.WriteStartElement("BcgMenus");
                reader.Read();
                while (reader.NodeType == XmlNodeType.Element)
                {
                    XmlReader inner = reader.ReadSubtree();
                    inner.Read();
                    if (inner.Name == "MapBrightnessMenu")
                    {
                        MakeBCG(inner, writer);
                    }
                    else if (inner.Name == "MapFilterMenu")
                    {
                        if (!flag)
                        {
                            writer.WriteEndElement();           // </BcgMenus>
                            writer.WriteStartElement("FilterMenus");
                            flag = true;
                        }
                        
                        MakeFilter(inner, writer);
                    }
                    inner.Close();
                    reader.Read();
                }
                writer.WriteEndElement();                       // </FilterMenus>
            }

            writer.WriteStartElement("GeoMaps");
            using (XmlReader reader = XmlReader.Create("Geomaps.xml", settings))
            {
                while (reader.ReadToFollowing("GeoMapRecord"))
                {
                    XmlReader mapReader = reader.ReadSubtree();
                    MakeGeoMap(writer, mapReader);
                    mapReader.Close();
                }
            }
            writer.WriteEndElement();                   //</GeoMaps>
            writer.WriteEndElement();                   //</GeoMapSet>
            writer.WriteEndDocument();
            writer.Close();

            Console.WriteLine("GeoMaps Created!");
        }

        static void MakeGeoMap(XmlWriter writer, XmlReader reader)
        {
            string gmName;
            string gmLabelLine1;
            string gmLabelLine2;
            string gmBcgMenuName;
            string gmFilterMenuName;

            // Read intial info about GeoMap
            reader.ReadToFollowing("GeomapId");
            gmName = reader.ReadElementContentAsString().Trim();
            Console.WriteLine("Importing Map '" + gmName + "' ");
            gmBcgMenuName = reader.ReadElementContentAsString().Trim();
            gmFilterMenuName = reader.ReadElementContentAsString().Trim();
            gmLabelLine1 = reader.ReadElementContentAsString().Trim();
            gmLabelLine2 = reader.ReadElementContentAsString().Trim();

            //Write GeoMap Element
            WriteGeoMapNode(gmName, gmLabelLine1, gmLabelLine2, gmBcgMenuName, gmFilterMenuName, writer);
            writer.WriteStartElement("Objects");

            // Read info about geomap elements
            while (reader.ReadToFollowing("GeoMapObjectType"))
            {
                XmlReader inner = reader.ReadSubtree();
                inner.ReadToDescendant("MapObjectType");
                string gmoType = inner.ReadElementContentAsString().Trim();

                if (gmoType != "SAA")
                {
                    string gmoID;
                    bool flag = false;

                    inner.Read();
                    gmoID = inner.Value.Trim();
                    inner.Read();


                    WriteGeoMapObjectNode(gmoType, gmoID, writer);

                    while (inner.Read() && inner.NodeType != XmlNodeType.EndElement)
                    {
                        XmlReader parser = inner.ReadSubtree();
                        parser.Read();
                        switch (parser.Name)
                        {
                            case "DefaultLineProperties":
                                // Obtain LineStyle, BCG Group, Color, Thickness
                                string lineStyle;
                                string bcgGroup;
                                string thickness;
                                List<string> filter = new List<string>();

                                parser.ReadToFollowing("LineStyle");
                                lineStyle = parser.ReadElementContentAsString().Trim();
                                bcgGroup = parser.ReadElementContentAsString().Trim();
                                parser.ReadToFollowing("Thickness");
                                thickness = parser.ReadElementContentAsString().Trim();
                                parser.ReadToFollowing("FilterGroup");
                                while (parser.Name == "FilterGroup")
                                {
                                    filter.Add(parser.ReadElementContentAsString().Trim());
                                }

                                WriteLineDefaultsNode(bcgGroup, lineStyle, thickness, filter, writer);

                                break;
                            case "TextDefaultProperties":
                                string bcg;
                                List<string> filters = new List<string>();
                                string fontSize;
                                string underline;
                                string displaySetting;
                                string xOffset;
                                string yOffset;

                                parser.ReadToFollowing("BCGGroup");
                                bcg = parser.ReadElementContentAsString().Trim();
                                parser.ReadToFollowing("FontSize");
                                fontSize = parser.ReadElementContentAsString().Trim();
                                underline = parser.ReadElementContentAsString().Trim();
                                displaySetting = parser.ReadElementContentAsString().Trim();
                                xOffset = parser.ReadElementContentAsString().Trim();
                                yOffset = parser.ReadElementContentAsString().Trim();
                                parser.ReadToFollowing("FilterGroup");
                                while (parser.Name == "FilterGroup")
                                {
                                    filters.Add(parser.ReadElementContentAsString().Trim());
                                }

                                WriteTextDefaultsNode(bcg, filters, fontSize, underline, displaySetting, xOffset, yOffset, writer);

                                break;
                            case "DefaultSymbolProperties":
                                string style;
                                string bcgG;
                                string font;
                                List<string> fil = new List<string>();

                                parser.ReadToFollowing("SymbolStyle");
                                style = parser.ReadElementContentAsString().Trim();
                                bcgG = parser.ReadElementContentAsString().Trim();
                                parser.ReadToFollowing("FontSize");
                                font = parser.ReadElementContentAsString().Trim();
                                parser.ReadToFollowing("FilterGroup");
                                while (parser.Name == "FilterGroup")
                                {
                                    fil.Add(parser.ReadElementContentAsString().Trim());
                                }

                                WriteSymbolDefaultsNode(bcgG, style, font, fil, writer);

                                break;
                            case "GeoMapLine":
                                // Get start lat, start long, end lat, end long
                                if (!flag)
                                {
                                    writer.WriteStartElement("Elements");
                                    flag = true;
                                }

                                string slat;
                                string elat;
                                string slong;
                                string elong;

                                parser.ReadToFollowing("StartLatitude");
                                slat = parser.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });
                                slong = parser.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });
                                elat = parser.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });
                                elong = parser.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });

                                WriteLineNode(slat, slong, elat, elong, writer);

                                break;
                            case "GeoMapText":
                                // Get lat, long, geotextstrings.textline
                                if (!flag)
                                {
                                    writer.WriteStartElement("Elements");
                                    flag = true;
                                }

                                string lat;
                                string _long;
                                string text;

                                parser.ReadToFollowing("Latitude");
                                lat = parser.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });
                                _long = parser.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });
                                parser.ReadToFollowing("TextLine");
                                text = parser.ReadElementContentAsString().Trim();

                                WriteTextNode(lat, _long, text, writer);

                                break;
                            case "GeoMapSymbol":
                                if (!flag)
                                {
                                    writer.WriteStartElement("Elements");
                                    flag = true;
                                }

                                string latitude;
                                string longitude;
                                string size = "";
                                string stext;
                                string id;

                                parser.ReadToFollowing("SymbolId");
                                id = parser.ReadElementContentAsString().Trim();

                                while (parser.Name != "Latitude")
                                    if (parser.Name == "FontSize")
                                        size = parser.ReadElementContentAsString().Trim();
                                    else
                                        parser.Skip();


                                latitude = parser.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });
                                longitude = parser.ReadElementContentAsString().Trim(new char[] { 'N', 'W' });

                                if (parser.ReadToFollowing("TextLine"))
                                    stext = parser.ReadElementContentAsString().Trim();
                                else
                                    stext = id;

                                WriteSymbolNode(size, latitude, longitude, writer);
                                WriteTextNode(latitude, longitude, stext, writer);

                                break;

                        }
                        parser.Close();

                    }

                    writer.WriteEndElement();           //</Elements>
                    writer.WriteEndElement();           //</GeoMapObject>

                }
                inner.Close();
            }
            writer.WriteEndElement();                   //</Objects>
            writer.WriteEndElement();                   //</GeoMap>
        }

        static void MakeBCG(XmlReader inner, XmlWriter writer)
        {
            string menuName;
            string label;

            inner.ReadToDescendant("BCGMenuName");
            menuName = inner.ReadElementContentAsString().Trim();
            Console.WriteLine("Importing BCG Menu '" + menuName + "' ");

            writer.WriteStartElement("BcgMenu");
            writer.WriteAttributeString("Name", menuName);

            writer.WriteStartElement("Items");
            while (inner.ReadToFollowing("Label"))
            {
                label = inner.ReadElementContentAsString().Trim();
                WriteBcgLabelNode(label, writer);
            }
            writer.WriteEndElement();           // </Items>
            writer.WriteEndElement();           // </BcgMenu>
        }

        static void MakeFilter(XmlReader inner, XmlWriter writer)
        {
            string menuName;
            string line1;

            inner.ReadToFollowing("FilterMenuName");
            menuName = inner.ReadElementContentAsString().Trim();
            Console.WriteLine("Importing Filter Menu '" + menuName + "' ");

            writer.WriteStartElement("FilterMenu");
            writer.WriteAttributeString("Name", menuName);

            writer.WriteStartElement("Items");
            while (inner.ReadToFollowing("LabelLine1"))
            {
                string line2 = "";

                line1 = inner.ReadElementContentAsString().Trim();

                if (inner.Name == "LabelLine2")
                {
                    line2 = inner.ReadElementContentAsString().Trim();
                }

                WriteFilterLabelNode(line1, line2, writer);
            }
            writer.WriteEndElement();           // </Items>
            writer.WriteEndElement();           // </FilterMenu>
        }

        /// <summary>
        /// Deals with writing information as Xml nodes
        /// </summary>

        static void WriteBcgLabelNode(string label, XmlWriter writer)
        {
            writer.WriteStartElement("BcgMenuItem");
            writer.WriteAttributeString("Label", label);
            writer.WriteEndElement();
        }

        static void WriteFilterLabelNode(string line1, string line2, XmlWriter writer)
        {
            writer.WriteStartElement("FilterMenuItem");
            writer.WriteAttributeString("LabelLine1", line1);
            writer.WriteAttributeString("LabelLine2", line2);
            writer.WriteEndElement();
        }

        static void WriteLineNode(string sLat, string sLong, string eLat, string eLong, XmlWriter writer)
        {
            if (eLong[eLong.Length - 1] == 'E')
            {
                eLong = eLong.Trim(new char[] { 'E' });
                eLong = (int.Parse(eLong) * -1).ToString();
            }

            if (eLat[eLat.Length - 1] == 'S')
            {
                eLat = eLat.Trim(new char[] { 'S' });
                eLat = (int.Parse(eLat) * -1).ToString();
            }

            if (sLong[sLong.Length - 1] == 'E')
            {
                sLong = sLong.Trim(new char[] { 'E' });
                sLong = (int.Parse(sLong) * -1).ToString();
            }

            if (sLat[sLat.Length - 1] == 'S')
            {
                sLat = sLat.Trim(new char[] { 'S' });
                sLat = (int.Parse(sLat) * -1).ToString();
            }

            writer.WriteStartElement("Element");
            writer.WriteAttributeString("type", "http://www.w3.org/2001/XMLSchema-instance" ,"Line");
            writer.WriteAttributeString("Filters", "");
            writer.WriteAttributeString("StartLat", ConvertCoordinate(int.Parse(sLat)).ToString());
            writer.WriteAttributeString("StartLon", ConvertCoordinate((-1 * int.Parse(sLong))).ToString());
            writer.WriteAttributeString("EndLat", ConvertCoordinate(int.Parse(eLat)).ToString());
            writer.WriteAttributeString("EndLon", ConvertCoordinate((-1 * int.Parse(eLong))).ToString());
            writer.WriteEndElement();
        }

        static void WriteTextNode(string lat, string _long, string text, XmlWriter writer)
        {
            if (_long[_long.Length - 1] == 'E')
            {
                _long = _long.Trim(new char[] { 'E' });
                _long = (int.Parse(_long) * -1).ToString();
            }

            if (lat[lat.Length - 1] == 'S')
            {
                lat = lat.Trim(new char[] { 'S' });
                lat = (int.Parse(lat) * -1).ToString();
            }

            writer.WriteStartElement("Element");
            writer.WriteAttributeString("type", "http://www.w3.org/2001/XMLSchema-instance" ,"Text");
            writer.WriteAttributeString("Filters", "");
            writer.WriteAttributeString("Lat", ConvertCoordinate(int.Parse(lat)).ToString());
            writer.WriteAttributeString("Lon", ConvertCoordinate((-1 * int.Parse(_long))).ToString());
            writer.WriteAttributeString("Lines", text);
            writer.WriteEndElement();
        }

        static void WriteSymbolNode(string size, string lat, string longitude, XmlWriter writer)
        {
            writer.WriteStartElement("Element");
            writer.WriteAttributeString("type", "http://www.w3.org/2001/XMLSchema-instance", "Symbol");
            writer.WriteAttributeString("Filters", "");

            if (size != "")
                writer.WriteAttributeString("Size", size);

            if (longitude[longitude.Length - 1] == 'E')
            {
                longitude = longitude.Trim(new char[] { 'E' });
                longitude = (int.Parse(longitude) * -1).ToString();
            }

            if (lat[lat.Length - 1] == 'S')
            {
                lat = lat.Trim(new char[] { 'S' });
                lat = (int.Parse(lat) * -1).ToString();
            }

            writer.WriteAttributeString("Lat", ConvertCoordinate(int.Parse(lat)).ToString());
            writer.WriteAttributeString("Lon", ConvertCoordinate((-1 * int.Parse(longitude))).ToString());
            writer.WriteEndElement();
        }

        static void WriteLineDefaultsNode(string bcg, string style, string thickness, List<string> filters, XmlWriter writer)
        {
            string fil;

            if (filters.Count > 0)
            {
                fil = string.Join(",", filters);
            }
            else
            {
                fil = "20";
            }

            if (filters.Count >= 3)
            {
                ;
            }

            writer.WriteStartElement("LineDefaults");
            writer.WriteAttributeString("Bcg", bcg);
            writer.WriteAttributeString("Filters", fil);
            writer.WriteAttributeString("Style", style);
            writer.WriteAttributeString("Thickness", thickness);
            writer.WriteEndElement();
        }

        static void WriteTextDefaultsNode(string bcg, List<string> filters, string size, string underline, string opaque, string xoffset, string yoffset, XmlWriter writer)
        {
            string fil;

            if (filters.Count > 0)
            {
                fil = string.Join(",", filters);
            }
            else
            {
                fil = "20";
            }

            if (filters.Count >= 3)
            {
                ;
            }

            writer.WriteStartElement("TextDefaults");
            writer.WriteAttributeString("Bcg", bcg);
            writer.WriteAttributeString("Filters", fil);
            writer.WriteAttributeString("Size", size);
            writer.WriteAttributeString("Underline", underline);
            writer.WriteAttributeString("Opaque", opaque);
            writer.WriteAttributeString("XOffset", xoffset);
            writer.WriteAttributeString("YOffset", yoffset);
            writer.WriteEndElement();
        }

        static void WriteSymbolDefaultsNode(string bcg, string style, string size, List<string> filters, XmlWriter writer)
        {
            string fil;

            if (filters.Count > 0)
            {
                fil = string.Join(",", filters);
            }
            else
            {
                fil = "20";
            }

            if (filters.Count >= 3)
            {
                ;
            }

            writer.WriteStartElement("SymbolDefaults");
            writer.WriteAttributeString("Bcg", bcg);
            writer.WriteAttributeString("Filters", fil);
            writer.WriteAttributeString("Style", style);
            writer.WriteAttributeString("Size", size);
            writer.WriteEndElement();
        }

        static void WriteGeoSetNode(XmlWriter writer)
        {
            writer.WriteStartElement("GeoMapSet");
            writer.WriteAttributeString("DefaultMap", "CERTMAP");    //// May not always be default map!!!
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
        }

        static void WriteGeoMapNode(string name, string ll1, string ll2, string bcgname, string fmname, XmlWriter writer)
        {
            writer.WriteStartElement("GeoMap");
            writer.WriteAttributeString("Name", name);
            writer.WriteAttributeString("LabelLine1", ll1);
            writer.WriteAttributeString("LabelLine2", ll2);
            writer.WriteAttributeString("BcgMenuName", bcgname);
            writer.WriteAttributeString("FilterMenuName", fmname);
        }

        static void WriteGeoMapObjectNode(string name, string id, XmlWriter writer)
        {
            writer.WriteStartElement("GeoMapObject");
            writer.WriteAttributeString("Description", "Object #" + id + " (" + name + ")");
            writer.WriteAttributeString("TdmOnly", "false");
        }

        static void WriteRadarSiteNode(string name, string elevation, string primaryRange, string secondaryRange, string lat, string lon, XmlWriter writer)
        {
            writer.WriteStartElement("RadarSite");
            writer.WriteAttributeString("IsPrimary", "false");
            writer.WriteAttributeString("ID", name);
            writer.WriteAttributeString("Elevation", elevation);
            writer.WriteAttributeString("FloorSlope", "0.3");
            writer.WriteAttributeString("PrimaryRange", primaryRange);
            writer.WriteAttributeString("SecondaryRange", secondaryRange);
            if (name == "RBL" || name == "QMV" || name == "SAC")
                writer.WriteAttributeString("ReducedSeparationRange", "40");
            else
                writer.WriteAttributeString("ReducedSeparationRange", "0");
            writer.WriteAttributeString("ConeOfSilenceSlope", "15");
            writer.WriteAttributeString("IsTerminal", "false");

            writer.WriteStartElement("Location");
            writer.WriteAttributeString("Lon", lon);
            writer.WriteAttributeString("Lat", lat);
            writer.WriteEndElement();           // </Location>
            writer.WriteEndElement();           // </RadarSite>
        }

        static void WriteLocalNode(XmlWriter writer)
        {
            writer.WriteStartElement("Local");
            writer.WriteStartElement("NetworkRating");
            writer.WriteString("C1");
            writer.WriteEndElement();           // </NetworkRating>

            writer.WriteStartElement("NetworkFacility");
            writer.WriteString("CTR");
            writer.WriteEndElement();           // </NetworkFacility>

            writer.WriteStartElement("DefaultCallsign");
            writer.WriteString("OAK_CTR");
            writer.WriteEndElement();           // </DefaultCallsign>

            writer.WriteStartElement("ObserverCallsign");
            writer.WriteString("ZOA_XX_OBS");
            writer.WriteEndElement();           // </ObserverCallsign>

            writer.WriteStartElement("ControllerInfo");
            writer.WriteEndElement();           // </ControllerInfo>

            writer.WriteStartElement("RequestedAltimeters");
            writer.WriteEndElement();           // </RequestedAltimeters>

            writer.WriteStartElement("RequestedWeatherReports");
            writer.WriteEndElement();           // </RequestedWeatherReports>

            writer.WriteStartElement("SelectedBeaconCodes");
            writer.WriteEndElement();           // </SelectedBeaconCodes>

            writer.WriteStartElement("DisplaySettings");
            writer.WriteEndElement();           // </DisplaySettings>
            writer.WriteEndElement();           // </Local>
        }

        /// <summary>
        /// Deals with calculations
        /// </summary>

        static double ConvertCoordinate(int DMS)
        {
            int degrees;
            int minutes;
            double seconds;

            seconds = DMS % 10000;
            seconds /= 100.0;
            DMS /= 10000;
            minutes = DMS % 100;
            DMS /= 100;
            degrees = DMS;

            return Math.Round(degrees + (minutes / 60.0) + (seconds / 3600.0), 6);
        }

        /// <summary>
        /// Deals with output to the user console
        /// </summary>
        static void PrintHeader()
        {
            Console.WriteLine("       _____");
            Console.WriteLine(" ______\\---/");
            Console.WriteLine(" \\         /          __________          ____________");
            Console.WriteLine("  \\       /                    /         /            \\               /\\");
            Console.WriteLine("   \\     /                    /         /              \\             /  \\");
            Console.WriteLine("    \\    |                   /         /                \\           /    \\");
            Console.WriteLine("    |    |                  /         /                  \\         /      \\");
            Console.WriteLine("    |    |                 /         /                    \\       /        \\");
            Console.WriteLine("    |    |                /          \\                    /      /----------\\");
            Console.WriteLine("    |    |               /            \\                  /      /            \\");
            Console.WriteLine("    |    |     \\  /     /              \\                /      /              \\");
            Console.WriteLine("    |    |      \\/     /                \\              /      /                \\");
            Console.WriteLine("    |    |            /_________         \\____________/      /                  \\");
            Console.WriteLine("    |____|");
            Console.WriteLine();
            Console.WriteLine("vERAM Facility Configuration Creator v1.0");
            Console.WriteLine("Copyright 2018 Oakland ARTCC on VATSIM. All rights reserved.");
            Console.WriteLine("Created by Srinath Nandakumar in July, 2018");
            Console.WriteLine("Use for simulation purposes only.");
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}

