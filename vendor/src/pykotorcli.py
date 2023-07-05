import os
import sys

from enum import Enum
from pykotor.tslpatcher.config import ModInstaller
from pykotor.tslpatcher.reader import NamespaceReader

# Override the print function to flush immediately
def custom_print(*args, **kwargs):
    builtins_print(*args, **kwargs)
    sys.stdout.flush()

# Replace the built-in print function
builtins_print = print
print = custom_print

# Replace the print function in the imported modules
sys.modules['pykotor.tslpatcher.reader'].print = custom_print
sys.modules['pykotor.tslpatcher.config'].print = custom_print


class ExitCode(Enum):
    Success = 0
    NumberOfArgs = 1
    NamespacesIniNotFound = 2
    NamespaceIndexOutOfRange = 3
    ChangesIniNotFound = 4

if len(sys.argv) < 3 or len(sys.argv) > 4:
    print("Syntax: pykotorcli.exe [\"\\path\\to\\game\\dir\"] [\"\\path\\to\\tslpatchdata\"] {\"namespace_option_index\"}")
    sys.exit(ExitCode.NumberOfArgs)

game_path = sys.argv[1]
tslpatchdata_path = sys.argv[2]
namespace_index = None
changes_ini_path = None

if len(sys.argv) == 3:
    changes_ini_path = os.path.join(tslpatchdata_path, "tslpatchdata", "changes.ini")
elif len(sys.argv) == 4:
    try:
        namespace_index = int(sys.argv[3])
    except ValueError:
        print("Invalid namespace_option_index. It should be an integer.")
        sys.exit(ExitCode.NamespaceIndexOutOfRange)

    namespaces_ini_path = os.path.join(tslpatchdata_path, "tslpatchdata", "namespaces.ini")
    print("Using namespaces.ini path: " + namespaces_ini_path)
    if not os.path.exists(namespaces_ini_path):
        print("The 'namespaces.ini' file was not found in the specified tslpatchdata path.")
        sys.exit(ExitCode.NamespacesIniNotFound)

    loaded_namespaces = NamespaceReader.from_filepath(namespaces_ini_path)
    if namespace_index is None or namespace_index >= len(loaded_namespaces):
        print("Namespace index is out of range.")
        sys.exit(ExitCode.NamespaceIndexOutOfRange)
    
    if loaded_namespaces[namespace_index].data_folderpath:
        changes_ini_path = os.path.join(
            tslpatchdata_path,
            "tslpatchdata",
            loaded_namespaces[namespace_index].data_folderpath,
            loaded_namespaces[namespace_index].ini_filename
        )
    else:
        changes_ini_path = os.path.join(
            tslpatchdata_path,
            "tslpatchdata",
            loaded_namespaces[namespace_index].ini_filename
        )
print("Using changes.ini path: " + changes_ini_path)
if not os.path.exists(changes_ini_path):
    print("The 'changes.ini' file could not be found.")
    sys.exit(ExitCode.ChangesIniNotFound)

mod_path = os.path.dirname(os.path.abspath(changes_ini_path))
ini_name = os.path.basename(changes_ini_path)

installer = ModInstaller(mod_path, game_path, ini_name)
installer.install()

print ("Writing log file 'installlog.txt'...")

log_file_path = os.path.join(tslpatchdata_path, "installlog.txt")
with open(log_file_path, "w") as log_file:
    for note in installer.log.notes:
        log_file.write(f"{note.message}\n")

    for warning in installer.log.warnings:
        log_file.write(f"Warning: {warning.message}\n")

    for error in installer.log.errors:
        log_file.write(f"Error: {error.message}\n")
        
print ("Logging finished")
sys.exit()