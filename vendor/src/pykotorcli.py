import argparse
import os
from pathlib import Path
import sys
# import cProfile

from enum import IntEnum
from pykotor.tslpatcher.config import ModInstaller
from pykotor.tslpatcher.reader import NamespaceReader


class ExitCode(IntEnum):
    SUCCESS = 0
    NUMBER_OF_ARGS = 1
    NAMESPACES_INI_NOT_FOUND = 2
    NAMESPACE_INDEX_OUT_OF_RANGE = 3
    CHANGES_INI_NOT_FOUND = 4


def parse_args():
    parser = argparse.ArgumentParser(description="TSLPatcher CLI written in PyKotor")

    # Positional arguments for the old syntax
    parser.add_argument("--game-dir", type=str, help="Path to game directory")
    parser.add_argument("--tslpatchdata", type=str, help="Path to tslpatchdata")
    parser.add_argument("--namespace-option-index", type=int, help="Namespace option index")

    # Add additional named arguments here if needed

    args, unknown = parser.parse_known_args()

    # If using the old syntax, we'll manually parse the first three arguments
    if len(unknown) >= 2:
        args.game_dir = unknown[0]
        args.tslpatchdata = unknown[1]
        if len(unknown) == 3:
            try:
                args.namespace_option_index = int(unknown[2])
            except ValueError:
                print("Invalid namespace_option_index. It should be an integer.")
                sys.exit(ExitCode.NAMESPACE_INDEX_OUT_OF_RANGE)

    return args


def main():
    args = parse_args()

    if args.game_dir is None or args.tslpatchdata is None:
        print("Syntax: pykotorcli.exe [\"\\path\\to\\game\\dir\"] [\"\\path\\to\\tslpatchdata\"] {\"namespace_option_index\"}")
        sys.exit(ExitCode.NUMBER_OF_ARGS)

    game_path: Path = Path(args.game_dir).resolve()          # argument 1
    tslpatchdata_path: Path = Path(args.tslpatchdata).resolve()  # argument 2
    namespace_index: int | None = None                       # argument 3
    changes_ini_path: Path

    if len(sys.argv) == 3:
        changes_ini_path = Path(
            tslpatchdata_path,
            "tslpatchdata",
            "changes.ini"
        ).resolve()
    elif len(sys.argv) == 4:
        namespace_index = int(args.namespace_option_index)
        changes_ini_path = determine_namespaces(
            tslpatchdata_path,
            namespace_index
        ).resolve()
    else:
        sys.exit(ExitCode.CHANGES_INI_NOT_FOUND)

    print("Using changes.ini path: " + str(changes_ini_path))
    if not changes_ini_path.exists():
        print(
            "The 'changes.ini' file does not exist"
            " anywhere in the tslpatchdata provided."
        )
        sys.exit(ExitCode.CHANGES_INI_NOT_FOUND)

    mod_path = changes_ini_path.parent
    ini_name = changes_ini_path.name

    installer = ModInstaller(mod_path, game_path, ini_name)

    # def profile_installation():
    #     installer.install()
    # cProfile.run('profile_installation()', 'output.prof')

    installer.install()

    print("Writing log file 'installlog.txt'...")
    log_file_path = os.path.join(tslpatchdata_path, "installlog.txt")
    with open(log_file_path, "w", encoding="utf-8") as log_file:
        for note in installer.log.notes:
            log_file.write(f"{note.message}\n")

        for warning in installer.log.warnings:
            log_file.write(f"Warning: {warning.message}\n")

        for error in installer.log.errors:
            log_file.write(f"Error: {error.message}\n")

    print("Logging finished")
    sys.exit(ExitCode.SUCCESS)


def determine_namespaces(tslpatchdata_path: Path, namespace_index: int) -> Path:
    try:
        namespace_index = int(sys.argv[3])
    except ValueError:
        print("Invalid namespace_option_index. It should be an integer.")
        sys.exit(ExitCode.NAMESPACE_INDEX_OUT_OF_RANGE)

    namespaces_ini_path: Path = Path(tslpatchdata_path, "tslpatchdata", "namespaces.ini").resolve()
    print("Using namespaces.ini path: {}".format(namespaces_ini_path))
    if not namespaces_ini_path.exists():
        print("The 'namespaces.ini' file was not found in the specified tslpatchdata path.")
        sys.exit(ExitCode.NAMESPACES_INI_NOT_FOUND)

    loaded_namespaces = NamespaceReader.from_filepath(str(namespaces_ini_path))
    if namespace_index >= len(loaded_namespaces):
        print("Namespace index is out of range.")
        sys.exit(ExitCode.NAMESPACE_INDEX_OUT_OF_RANGE)

    return (
        Path(
            tslpatchdata_path,
            "tslpatchdata",
            loaded_namespaces[namespace_index].data_folderpath,
            loaded_namespaces[namespace_index].ini_filename,
        )
        if loaded_namespaces[namespace_index].data_folderpath
        else Path(
            tslpatchdata_path,
            "tslpatchdata",
            loaded_namespaces[namespace_index].ini_filename,
        )
    )

main()
sys.exit()
