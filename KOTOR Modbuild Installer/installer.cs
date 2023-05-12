using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace KOTOR_Modbuild_Installer
{
    public class Instruction
    {
        public string Type { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
        public bool Overwrite { get; set; }
        public string Path { get; set; }
        public string Arguments { get; set; }
    }
    public class Component
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public int InstallOrder { get; set; }
        public List<string> Dependencies { get; set; }
        public List<Instruction> Instructions { get; set; }

        public void loadComponents()
        {
            string json = File.ReadAllText("components.json");
            List<Component> components = JsonConvert.DeserializeObject<List<Component>>(json);
        }

        public void something()
        {
            /*Bootstrapper bootstrapper = new Bootstrapper();
            foreach (Component component in components)
            {
                bootstrapper.Engine.StringVariables["ComponentName"] = component.Name;
                bootstrapper.Engine.StringVariables["ComponentGuid"] = component.Guid;
                bootstrapper.Engine.StringVariables["InstallOrder"] = component.InstallOrder.ToString();
                bootstrapper.Engine.StringVariables["Dependencies"] = string.Join(",", component.Dependencies);
                foreach (Instruction instruction in component.Instructions)
                {
                    switch (instruction.Type)
                    {
                        case "extract":
                            bootstrapper.Engine.DetectPackageComplete += (sender, e) =>
                            {
                                if (e.PackageId == "WixExtractFiles")
                                {
                                    bootstrapper.Engine.SetVariable("WixExtractFilesSource", instruction.Source);
                                    bootstrapper.Engine.SetVariable("WixExtractFilesTarget", instruction.Destination);
                                    bootstrapper.Engine.SetVariable("WixExtractFilesOverwrite", instruction.Overwrite ? "yes" : "no");
                                }
                            };
                            bootstrapper.Engine.Plan(LaunchAction.Install);
                            break;
                        case "delete":
                            bootstrapper.Engine.SetVariable("WixDeleteFilesPath", instruction.Path);
                            bootstrapper.Engine.Plan(LaunchAction.Uninstall);
                            break;
                        case "move":
                            bootstrapper.Engine.SetVariable("WixMoveFilesSource", instruction.Source);
                            bootstrapper.Engine.SetVariable("WixMoveFilesTarget", instruction.Destination);
                            bootstrapper.Engine.SetVariable("WixMoveFilesOverwrite", instruction.Overwrite ? "yes" : "no");
                            bootstrapper.Engine.Plan(LaunchAction.Install);
                            break;
                        case "run":
                            bootstrapper.Engine.SetVariable("WixRunExePath", instruction.Path);
                            bootstrapper.Engine.SetVariable("WixRunExeArguments", instruction.Arguments);
                            bootstrapper.Engine.Plan(LaunchAction.Install);
                            break;
                    }
                }
            }
            bootstrapper.Engine.Apply(IntPtr.Zero);*/
        }
        public void something2()
        {

        }
    }

}
