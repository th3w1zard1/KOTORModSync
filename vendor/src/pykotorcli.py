import os
import sys
import cProfile

from enum import IntEnum
from pykotor.tslpatcher.config import ModInstaller
from pykotor.tslpatcher.reader import NamespaceReader


class ExitCode(IntEnum):
    SUCCESS = 0
    NUMBER_OF_ARGS = 1
    NAMESPACES_INI_NOT_FOUND = 2
    NAMESPACE_INDEX_OUT_OF_RANGE = 3
    CHANGES_INI_NOT_FOUND = 4


if len(sys.argv) < 3 or len(sys.argv) > 4:
    print(
        "Syntax: pykotorcli.exe [\"\\path\\to\\game\\dir\"] [\"\\path\\to\\tslpatchdata\"] {\"namespace_option_index\"}")
    sys.exit(ExitCode.NUMBER_OF_ARGS)

GAME_PATH = sys.argv[1]
TSLPATCHDATA_PATH = sys.argv[2]
namespace_index = None
changes_ini_path = None

if len(sys.argv) == 3:
    changes_ini_path = os.path.join(
        TSLPATCHDATA_PATH, "tslpatchdata", "changes.ini")
elif len(sys.argv) == 4:
    try:
        namespace_index = int(sys.argv[3])
    except ValueError:
        print("Invalid namespace_option_index. It should be an integer.")
        sys.exit(ExitCode.NAMESPACE_INDEX_OUT_OF_RANGE)

    namespaces_ini_path = os.path.join(
        TSLPATCHDATA_PATH, "tslpatchdata", "namespaces.ini")
    print("Using namespaces.ini path: " + namespaces_ini_path)
    if not os.path.exists(namespaces_ini_path):
        print("The 'namespaces.ini' file was not found in the specified tslpatchdata path.")
        sys.exit(ExitCode.NAMESPACES_INI_NOT_FOUND)

    loaded_namespaces = NamespaceReader.from_filepath(namespaces_ini_path)
    if namespace_index >= len(loaded_namespaces):
        print("Namespace index is out of range.")
        sys.exit(ExitCode.NAMESPACE_INDEX_OUT_OF_RANGE)

    if loaded_namespaces[namespace_index].data_folderpath:
        changes_ini_path = os.path.join(
            TSLPATCHDATA_PATH,
            "tslpatchdata",
            loaded_namespaces[namespace_index].data_folderpath,
            loaded_namespaces[namespace_index].ini_filename
        )
    else:
        changes_ini_path = os.path.join(
            TSLPATCHDATA_PATH,
            "tslpatchdata",
            loaded_namespaces[namespace_index].ini_filename
        )
if changes_ini_path is None:
    sys.exit(ExitCode.CHANGES_INI_NOT_FOUND)

print("Using changes.ini path: " + changes_ini_path)
if not os.path.exists(changes_ini_path):
    print("The 'changes.ini' file could not be found.")
    sys.exit(ExitCode.CHANGES_INI_NOT_FOUND)

mod_path = os.path.dirname(os.path.abspath(changes_ini_path))
ini_name = os.path.basename(changes_ini_path)

installer = ModInstaller(mod_path, GAME_PATH, ini_name)


#def profile_installation():
#    installer.install()
# cProfile.run('profile_installation()', 'output.prof')

installer.install()

print("Writing log file 'installlog.txt'...")
log_file_path = os.path.join(TSLPATCHDATA_PATH, "installlog.txt")
with open(log_file_path, "w", encoding="utf-8") as log_file:
    for note in installer.log.notes:
        log_file.write(f"{note.message}\n")

    for warning in installer.log.warnings:
        log_file.write(f"Warning: {warning.message}\n")

    for error in installer.log.errors:
        log_file.write(f"Error: {error.message}\n")

print("Logging finished")
sys.exit()
