using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using System.Xml;

namespace ModbuildInstaller
{
    internal class ReadConfig
    {
        public static void ReadConfigFile(string filePath)
        {
            // Create a new XmlReaderSettings object to enable reading of DTDs
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.DtdProcessing = DtdProcessing.Parse;

            // Create a new XmlReader object to read the XML file
            using (XmlReader reader = XmlReader.Create("config.xml", readerSettings))
            {
                // Load the XML file into an XmlDocument object
                XmlDocument doc = new XmlDocument();
                doc.Load(reader);

                // Find all "Feature" nodes in the document
                XmlNodeList featureNodes = doc.SelectNodes("//Feature");

                // Loop through each "Feature" node and retrieve the necessary attributes
                foreach (XmlNode featureNode in featureNodes)
                {
                    string id = featureNode.Attributes["Id"].Value;
                    string name = featureNode.Attributes["Name"].Value;
                    string description = featureNode.Attributes["Description"].Value;
                    string installMethod = featureNode.Attributes["InstallMethod"].Value;

                    // Install files and dependencies as specified in configuration file
                }
            }
        }
    }
}
