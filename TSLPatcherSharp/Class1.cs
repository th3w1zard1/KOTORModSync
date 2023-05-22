using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSLPatcher
{
    public static class Functions
    {
        public const int LOG_LEVEL_VERBOSE = 1;
        public const int LOG_LEVEL_ERROR = 2;
        public const int LOG_LEVEL_ALERT = 3;
        public const int LOG_LEVEL_INFORMATION = 4;
        public const int LOG_LEVEL_NOTICE = 5;
        public const int ACTION_ADD_ROW = 1;
        public const int ACTION_MODIFY_ROW = 2;
        public const int ACTION_COPY_ROW = 3;
        public const int ACTION_ADD_COLUMN = 4;
        public const int ACTION_ADD_FIELD = 5;
        public const int TLK_TYPE_NORMAL = 1;
        public const int TLK_TYPE_FEMALE = 2;

        private static string user = Environment.UserName;
        private static string baseDir;
        private static int game1 = 0;
        private static int game2 = 0;
        private static string pathgame1 = string.Empty;
        private static string pathgame2 = string.Empty;
        private static GUI GUIInstance;

        private static string installPath;
        private static string installInfo = "info.rtf";
        private static string installIni = "changes.ini";
        private static string installDestPath;
        private static ConfigIniMan iniObject;
        private static ConfigIniMan uninstallIni;
        private static bool bInstalled = false;

        private static Dictionary<string, string> tokens = new Dictionary<string, string>();
        private static List<string> twodaTokens = new List<string>();
        private static List<string> ERFs = new List<string>();

        private static Dictionary<string, object> installInfoDict = new Dictionary<string, object>
        {
            { "FileExists", 0 },
            { "bInstall", 0 },
            { "sMessage", Messages["LS_GUI_DEFAULTCONFIRMTEXT"] },
            { "iLoglevel", 3 },
            { "bLogOld", 0 }
        };

        private static Dictionary<string, object> scriptInfoDict = new Dictionary<string, object>
        {
            { "OrgFile", null },
            { "ModFile", null },
            { "IsInclude", null }
        };

        private static int logAlerts = 0;
        private static int logErrors = 0;
        private static int logCount = 0;
        private static int logIndex = 0;
        private static string logText = "";
        private static int logTextDone = 0;
        private static bool logFirst = true;

        private static int twodaAddNum;
        private static int twodaChanum;
        private static int twodaColNum;
        private static int twodaDelColNum;
        private static int twodaDelNum;

        private static int gffDelfNum;
        private static int gffDelNum;
        private static int gffRepNum;

        // Patcher Messages
        private static Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            // Miscellaneous
            {"LS_GUI_CONFIGMISSING", "WARNING! Cannot locate the INI file \"%s1\" with work instructions!"},
            {"LS_GUI_DEFAULTCAPTION", "Game Data Patcher for KotOR/TSL"},
            {"LS_GUI_SBARINSTALLDEST", "Game folder: %s1"},
            {"LS_GUI_SBARINSTALLUSERSEL", "User selected."},
            {"LS_GUI_INFOLOADERROR", "Unable to load the instructions text! Make sure the \"tslpatchdata\" folder containing the \"%s1\" file is located in the same folder as this application."},
            {"LS_GUI_BUTTONCAPINSTALL", "Install Mod"},
            {"LS_GUI_BUTTONCAPPATCH", "Start patching"},
            {"LS_GUI_CONFIGLOADERROR", "Unable to load the %s1 file! Make sure the \"tslpatchdata\" folder is located in the same folder as this application."},
            {"LS_GUI_DEFAULTCONFIRMTEXT", "This will start patching the necessary game files. Do you want to do this?"},
            {"LS_GUI_SUMMARY", "The Installer is finished. Please check the progress log for details about what has been done."},
            {"LS_GUI_SUMMARYWARN", "The Installer is finished, but %s1 warnings were encountered! The Mod may or may not be properly installed. Please check the progress log for further details."},
            {"LS_GUI_SUMMARYERROR", "The Installer is finished, but %s1 errors were encountered! The Mod has likely not been properly installed. Please check the progress log for further details."},
            {"LS_GUI_SUMMARYERRORWARN", "The Installer is finished, but %s1 errors and %s2 warnings were encountered! The Mod most likely has not been properly installed. Please check the progress log for further details."},
            {"LS_GUI_PSUMMARY", "The Patcher is finished. Please check the progress log for details about what has been done."},
            {"LS_GUI_PSUMMARYWARN", "The Patcher is finished, but %s1 warnings were encountered! The Mod may or may not be properly installed. Please check the progress log for further details."},
            {"LS_GUI_PSUMMARYERROR", "The Patcher is finished, but %s1 errors were encountered! The Mod has likely not been properly installed. Please check the progress log for further details."},
            {"LS_GUI_PSUMMARYERRORWARN", "The Patcher is finished, but %s1 errors and %s2 warnings were encountered! The Mod most likely has not been properly installed. Please check the progress log for further details."},
            {"LS_GUI_EXCEPTIONPREFIX", "An error occured! %s1"},
            {"LS_GUI_UEXCEPTIONPREFIX", "An unhandled error occured! "},
            {"LS_GUI_CONFIRMQUIT", "Are you sure you wish to quit?"},

            // Configuration summary
            {"LS_GUI_REPTITLE", "CONFIGURATION SUMMARY"},
            {"LS_GUI_REPSETTINGS", "Settings"},
            {"LS_GUI_REPCONFIGFILE", "Config file"},
            {"LS_GUI_REPINFOFILE", "Information file"},
            {"LS_GUI_REPINSTALLOC", "Install location"},
            {"LS_GUI_REPUSERSELECTED", "User selected."},
            {"LS_GUI_REPBACKUPS", "Make backups"},
            {"LS_GUI_REPDOBACKUPS", "Before modifying/overwriting existing files."},
            {"LS_GUI_REPNOBACKUPS", "Disabled, no backups are made."},
            {"LS_GUI_REPLOGLEVEL0", "Log level: 0 - No progress log"},
            {"LS_GUI_REPLOGLEVEL1", "Log level: 1 - Errors only"},
            {"LS_GUI_REPLOGLEVEL2", "Log level: 2 - Errors and warnings only"},
            {"LS_GUI_REPLOGLEVEL3", "Log level: 3 - Standard: Progress, errors and warnings."},
            {"LS_GUI_REPLOGLEVEL4", "Log level: 4 - Debug: Detailed progress, errors and warnings."},
            {"LS_GUI_REPTLKAPPEND", "dialog tlk appending"},
            {"LS_GUI_REPNEWTLKCOUNT", "New entries"},
            {"LS_GUI_REP2DATITLE", "2DA file changes"},
            {"LS_GUI_REP2DAFILE", "%s1 - new rows: %s2, modified rows: %s3, new columns: %s4"},
            {"LS_GUI_REPGFFTITLE", "GFF file changes"},
            {"LS_GUI_REPNONE", "none"},
            {"LS_GUI_REPOVERWRITE", "overwrite existing"},
            {"LS_GUI_REPMODIFY", "modify existing"},
            {"LS_GUI_REPSKIP", "skip existing"},
            {"LS_GUI_REPLOCATION", "location"},
            {"LS_GUI_REPHACKTITLE", "NCS file integer hacks"},
            {"LS_GUI_REPCOMPILETITLE", "Modified & recompiled scripts"},
            {"LS_GUI_REPSSFTITLE", "New/modified Soundset files"},
            {"LS_GUI_REPINSTALLSTART", "Unpatched files to install"},
            {"LS_GUI_REPINSTALLLOC", "Location"},
            {"LS_GUI_REPGAMEFOLDER", "Game folder"},
    
            // Run Patch Operation
            {"LS_LOG_RPOINSTALLSTART", "Installation started %s1..."},
            {"LS_LOG_RPOPATCHSTART", "Patch operation started %s1..."},
            {"LS_LOG_RPOSUMMARYWARN", "Done. Changes have been applied, but %s1 warnings were encountered."},
            {"LS_LOG_RPOSUMMARYERROR", "Done. Some changes may have been applied, but %s1 errors were encountered!"},
            {"LS_LOG_RPOSUMMARYWARNERROR", "Done. Some changes may have been applied, but %s1 errors and %s2 warnings were encountered!"},
            {"LS_LOG_RPOSUMMARY", "Done. All changes have been applied."},
            {"LS_LOG_RPOGENERALEXCEPTION", "Unhandled exception: %s1"},

            // TLK file handler
            {"LS_LOG_LOADINGSTRREFTOKENS", "Loading StrRef token table..."},
            {"LS_LOG_LOADEDSTRREFTOKENS", "%s1 StrRef tokens found and indexed."},
            {"LS_EXC_TLKFILETYPEMISMATCH", "Internal error, invalid TLK file type specified. This should never happen."},
            {"LS_LOG_APPENDFEEDBACK", "Appending strings to TLK file \"%s1\""},
            {"LS_LOG_TLKENTRYMATCHEXIST", "Identical string for append StrRef %s1 found in %s2 StrRef %s3, reusing it instead."},
            {"LS_LOG_APPENDTLKENTRY", "Appending new entry to %s1, new StrRef is %s2"},
            {"LS_LOG_MAKETLKBACKUP", "Saving unaltered backup copy of %s1 file in %s2"},
            {"LS_LOG_TLKSUMMARY1", "%s1 file updated with %s2 new entries, %s3 entries already existed."},
            {"LS_LOG_TLKSUMMARY2", "%s1 file updated with %s2 new entries."},
            {"LS_LOG_TLKSUMMARY3", "%s1 file not updated, all %s2 entries were already present."},
            {"LS_LOG_TLKSUMMARYWARNING", "Warning: No new entries appended to %s1. Possible missing entries in append.tlk referenced in the TLKList."},
            {"LS_LOG_TLKFILEMISSING", "Unable to load specified %s1 file! Aborting..."},
            {"LS_EXC_TLKFILEMISSING", "No TLK file loaded. Unable to proceed."},
            {"LS_LOG_TLKNOTSELECTED", "No %s1 file specified. Unable to proceed!"},
            {"LS_LOG_UNKNOWNSTRREFTOKEN", "Encountered StrRef token \"%s1\" in modifier list that was not present in the TLKList! Value set to StrRef #0."},

            // Install List Handler
            {"LS_LOG_INSSTART", "Installing unmodified files..."},
            {"LS_LOG_INSDESTINVALID", "Destination file \"%s1\" does not appear to be a valid ERF or RIM archive! Skipping section..."},
            {"LS_LOG_INSDESTNOTEXIST", "Destination file \"%s1\" does not exist at the specified location! Skipping section..."},
            {"LS_LOG_INSCREATEFOLDER", "Folder %s1 did not exist, creating it..."},
            {"LS_LOG_INSFOLDERCREATEFAIL", "Unable to create folder %s1! Skipping folder..."},
            {"LS_LOG_INSBACKUPFILE", "Saving unaltered backup copy of destination file %s1 in %s2."},
            {"LS_LOG_INSNOEXEPLEASE", "Skipping file %s1, this Installer will not overwrite EXE files!"},
            {"LS_LOG_INSENOUGHTLK", "Skipping file %s1, this Installer will not overwrite dialog.tlk directly."},
            {"LS_LOG_INSSKELETONKEY", "Skipping file %s1, this Installer will not overwrite the chitin.key file."},
            {"LS_LOG_INSBIFTHEUNDERSTUDY", "Skipping file %s1, this Installer will not overwrite BIF data files."},
            {"LS_LOG_INSREPLACERENAME", "Renaming and replacing file \"%s1\" to \"%s2\" in the %s folder..."},
            {"LS_LOG_INSREPLACE", "Replacing file %s1 in the %s2 folder..."},
            {"LS_LOG_INSLASKIP", "A file named %s1 already exists in the %s2 folder. Skipping file..."},
            {"LS_LOG_INSRENAMECOPY", "Renaming and copying file \"%s1\" to \"%s2\" to the %s3 folder..."},
            {"LS_LOG_INSCOPYFILE", "Copying file %s1 to the %s2 folder..."},
            {"LS_LOG_INSREPLACERENAMEFILE", "Renaming and replacing file \"%s1\" to \"%s2\" in the %s3 archive..."},
            {"LS_LOG_INSREPLACEFILE", "Replacing file %s1 in the %s2 archive..."},
            {"LS_LOG_INSEXCEPTIONSKIP", "%s Skipping..."},
            {"LS_LOG_INSLASKIPFILE", "A file named %s1 already exists in the %s2 archive. Skipping file..."},
            {"LS_LOG_INSRENAMEADDFILE", "Renaming and adding file \"%s1\" to \"%s2\" in the %s3 archive..."},
            {"LS_LOG_INSADDFILE", "Adding file %s1 to the %s2 archive..."},
            {"LS_LOG_INSCOPYFAILED", "Unable to copy file \"%s1\", file does not exist!"},
            {"LS_LOG_INSNOMODIFIERS", "No install instructions (%s1) found for folder %s2."},
            {"LS_LOG_INSINVALIDDESTINATION", "Invalid install location \"%s1\" encountered! Skipping..."},

            // 2DA Handler
            {"LS_LOG_2DAFILENOTFOUND", "Unable to find 2DA file \"%s1\" to modify! Skipping file..."},
            {"LS_LOG_2DAINVALIDMODIFIER", "Invalid modifier type \"%s1\" found for modifier label \"%s2\". Skipping..."},
            {"LS_LOG_2DABACKUPFILE", "Saving unaltered backup copy of %s1 in %s2"},
            {"LS_LOG_2DAFILEUPDATED", "Updated 2DA file %s1."},
            {"LS_LOG_2DALOADERROR", "Unable to load the 2DA file %s1! Skipping it..."},
            {"LS_LOG_2DANOFILESELECTED", "No %s1 file was specified! Skipping it..."},
            {"LS_LOG_EXCLUSIVECOLINVALID", "Invalid Exclusive column label \"%s1\" specified, ignoring..."},
            {"LS_LOG_EXCLUSIVEMATCHFOUND", "Matching value in column %s1 found for existing row %s2..."},
            {"LS_LOG_NOEXCLUSIVEVALUESET", "No value has been assigned to column %s1 for new 2DA line in modifier \"%s2\" with Exclusive checking enabled! Skipping line..."},
            {"LS_LOG_2DAEXROWNOTFOUND", "Error locating row when trying to modify existing Exclusive row in modifier \"%s1\"."},
            {"LS_LOG_2DAEXROWINDEXTOOHIGH", "Too high row-number encountered when trying to modify existing Exclusive row in modifier \"%s1\"."},
            {"LS_LOG_2DAEXROWMATCH", "New Exclusive row matched line %s1 in 2DA file %s2, modifying existing line instead."},
            {"LS_LOG_2DAINVALIDCOLLABEL", "Invalid column label \"%s1\" encountered! Skipping entry..."},
            {"LS_LOG_2DAHIGHTOKENRLFOUND", "Setting row label to next HIGHEST value %s1."},
            {"LS_LOG_2DAADDINGROW", "Adding new row (index %s1) to 2DA file %s2..."},
            {"LS_LOG_2DASETROWLABELERROR", "Unable to set new row label \"%s1\" in modifier + \"%s2\"!"},
            {"LS_LOG_2DAHIGHTOKENVALUE", "Setting added row column %s1 to next HIGHEST value %s2."},
            {"LS_LOG_2DAADDROWERROR", "An error occured while trying to add new line to 2DA in modifier \"%s1\"!"},
            {"LS_LOG_2DANOLABELCOL", "%s1 used as index when changing line in modifier \"%s2\" but 2DA file has no label column! Skipping..."},
            {"LS_LOG_2DANONEXCLUSIVECOL", "Warning, multiple rows matching Label Index found! Last found row will be used..."},
            {"LS_LOG_2DAMULTIMATCHINDEX", "Multiple matches for specified Label Index, previously found row %s1, now found row %s2."},
            {"LS_LOG_2DAMODIFYLINE", "Modifying line (index %s1) in 2DA file %s2..."},
            {"LS_LOG_2DANOINDEXFOUND", "No RowIndex/RowLabel identifier for row to modify found at top of modifier list! Unable to apply modifier \"%s1\"."},
            {"LS_LOG_2DAADDCOLUMN", "Adding new column to 2DA file %s1..."},
            {"LS_LOG_2DACOLEXISTS", "A column with the label \"%s1\" already exists in %s1, unable to add new column!"},
            {"LS_LOG_2DAINVALIDROWLABEL", "Invalid row label %s1 encountered! Skipping entry..."},
            {"LS_LOG_2DANEWROWLABELHIGH", "Setting new row label to next HIGHEST value %s1."},
            {"LS_LOG_2DACOPYFAILED", "Error! Failed to copy line in 2DA! Skipping..."},
            {"LS_LOG_2DACOPYINGLINE", "Copying line %s1 to new line %s2 in %s3."},
            {"LS_LOG_2DAINCTOPENCOPY", "Incrementing value of copied row for column %s1 by %s2, new value is %s3."},
            {"LS_LOG_2DAINCFAILED", "Row value increment failed! Specified modifier \"%s1\" is not a number. Old row value not changed."},
            {"LS_LOG_2DAINCFAILEDNONUM", "Row value increment failed! Specified row column does not contain a number. Old row value not changed."},
            {"LS_LOG_2DACOPYHIGH", "Setting copied row column %s1 to next HIGHEST value %s2."},

            // 2DAMEMORY token handler
            {"LS_LOG_TOKENERROR1", "Invalid 2DAMEMORY token found! Token indexes start at 1 and go up..."},
            {"LS_LOG_TOKENERROR2", "Invalid memory token %s1 encountered, using first memory slot instead."},
            {"LS_LOG_TOKENFOUND", "Found a %s1 token! Storing value \"%s2\" from 2da to memory..."},
            {"LS_LOG_TOKENLABELERROR", "Error looking up row label for row index %s1"},
            {"LS_LOG_TOKENCOLUMNERROR", "Invalid column label \"%s1\" passed to %s2 key!"},
            {"LS_LOG_TOKENCOLLABELERROR", "Error looking up column label for column index %s1"},
            {"LS_LOG_INVALIDCOLLABEL", "Invalid column label passed to %s1 key!"},
            {"LS_LOG_TOKENROWLERROR", "Invalid row label \"%s1\" passed to %s2 key!"},
            {"LS_LOG_LINDEXTOKENFOUND", "Found a %s1 token! Storing ListIndex \"%s2\" from GFF to memory..."},
            {"LS_LOG_FPATHTOKENFOUND", "Found a %s1 token! Storing Field Path \"%s2\" from GFF to memory..."},
            {"LS_LOG_TOKENINDEXERROR1", "Invalid memory token %s1 encountered, assuming first memory slot."},
            {"LS_LOG_TOKENINDEXERROR2", "Invalid memory token %s1 encountered, unable to insert a proper value into cell or field!"},
            {"LS_LOG_GETTOKENVALUE", "Found a %s1 value, substituting with value \"%s2\" in memory..."},

            // Override fileexists check and response
            {"LS_LOG_OVRCHECKNOFILE", "Override check: No file with name \"%s1\" found in override folder."},
            {"LS_LOG_OVRCHECKEXISTWARN", "A file named %s1 already exists in the override folder! This may cause incompatibility with the one used by this mod!"},
            {"LS_LOG_OVRCHECKRENAMED", "A file named %s1 already existed in the override folder! This existing file has been renamed to %s2 to allow the one in this Mod to be used!"},
            {"LS_LOG_OVRRENAMEFAILED", "A file named %s1 already exists in the override folder! Renaming existing file to %s2 failed! The file might be write-protected or a file with the new name already exist."},
            {"LS_LOG_OVRCHECKSILENTWARN", "Warning: A file named %s1 already exists in the override folder. It will override the one in the ERF/RIM archive in-game."},

            // GFF file handler
            {"LS_LOG_GFFSECTIONMISSING", "Unable to locate section \"%s1\" when attempting to add GFF Field, skipping..."},
            {"LS_LOG_GFFPARENTALERROR", "Parent field at \"%s1\" does not exist or is not a LIST or STRUCT! Unable to add new Field \"%s2\"..."},
            {"LS_LOG_GFFMISSINGLABEL", "No field label has been specified for new field in section \"%s1\"! Unable to create field..."},
            {"LS_LOG_GFFLABELEXISTS", "A Field with the label \"%s1\" already exists at \"%s2\", skipping it..."},
            {"LS_LOG_GFFLABELEXISTSMOD", "A Field with the label \"%s1\" already exists at \"%s2\", modifying instead..."},
            {"LS_LOG_GFFINVALIDSTRREF", "Invalid StrRef value \"%s1\" when attempting to add ExoLocString. Defaulting to -1..."},
            {"LS_LOG_GFFINVALIDTYPEDATA", "Invalid field type \"%s1\" or data specified in section \"%s2\" when trying to add fields to %s3, skipping..."},
            {"LS_LOG_GFFADDEDSTRUCT", "Added %s1, index %s2, at position \"%s3\""},
            {"LS_LOG_GFFADDEDFIELD", "Added %s1 field \"%s2\" at position \"%s3\""},
            {"LS_LOG_GFFPROCSUBFIELDS", "Processing new sub-fields at %s1."},
            {"LS_LOG_GFFMODIFYING", "Modifying GFF format files..."},
            {"LS_LOG_GFFNOINSTRUCTION", "No instruction section found for file %s1, skipping..."},
            {"LS_LOG_GFFMODIFYINGFILE", "Modifying GFF file %s1..."},
            {"LS_LOG_GFFBLANKFIELDLABEL", "Blank Gff Field Label encountered in instructions, skipping..."},
            {"LS_LOG_GFFNEWFIELDADDED", "Added new field to GFF file %s1..."},
            {"LS_LOG_GFFBLANKVALUE", "Blank value encountered for GFF field label %s1, skipping..."},
            {"LS_LOG_GFFMODIFIEDVALUE", "Modified value \"%s1\" to field \"%s2\" in %s3."},
            {"LS_LOG_GFFINCORRECTLABEL", "Unable to find a field label matching \"%s1\" in %s2, skipping..."},
            {"LS_LOG_GFFBACKUPFILE", "Saving unaltered backup copy of %s1 file in %s2"},
            {"LS_LOG_GFFBACKUPDEST", "Saving unaltered backup copy of destination file %s1 file in %s2"},
            {"LS_LOG_GFFMODFIELDSUMMARY", "Modified %s1 fields in \"%s2\"..."},
            {"LS_LOG_GFFINSERTDONE", "Finished updating GFF file \"%s1\" in \"%s2\"..."},
            {"LS_LOG_GFFSAVEINERFORRIM", "Saving modified file \"%s1\" in archive \"%s2\"."},
            {"LS_LOG_GFFUPDATEFINISHED", "Finished updating GFF file \"%s1\"..."},
            {"LS_LOG_GFFNOCHANGES", "No changes could be applied to GFF file %s1."},
            {"LS_LOG_GFFNOMODIFIERS", "No GFF modifier instructions found for file %s1, skipping..."},
            {"LS_LOG_GFFCANTLOADFILE", "Unable to load file %s1! Skipping..."},
            {"LS_LOG_GFFNOFILEOPENED", "No valid %s1 file was opened, skipping..."},
            {"LS_LOG_GFFMISSINGLISTSTRUCT", "Could not find struct to modify in parent list at %s1, unable to add new field!"},

            // HACK List handler
            {"LS_LOG_HAKSTART", "Modifying binary files..."},
            {"LS_LOG_HAKMODIFYFILE", "Modifying binary file \"%s1\"..."},
            {"LS_LOG_HAKNOOFFSETS", "No offsets found for file %s1, skipping..."},
            {"LS_LOG_HAKNOVALIDFILE", "No valid %s1 file found! Skipping file."},
            {"LS_LOG_HAKBACKUPFILE", "Saving unaltered backup copy of %s1 in %s2."},
            {"LS_LOG_HAKMODIFYINGDATA", "Modifying file %s1, setting value at offset \"%s2\" to \"%s3\"."},
            {"LS_LOG_HAKINVALIDOFFSET", "Invalid offset(%s1) or value(%s2) modifier for file %s3. Skipping..."},

            // Recompile file handler
            {"LS_LOG_NCSBEGINNING", "Modifying and compiling scripts..."},
            {"LS_LOG_NCSCOMPILERMISSING", "Could not locate nwnsscomp.exe in the tslpatchdata folder! Unable to compile scripts!"},
            {"LS_LOG_NCSPROCESSINGTOKENS", "Replacing tokens in script %s1..."},
            {"LS_LOG_NCSCOMPILINGSCRIPT", "Compiling modified script %s1..."},
            {"LS_LOG_NCSCOMPILEROUTPUT", "NWNNSSComp says: %s1"},
            {"LS_LOG_NCSDESTBACKUP", "Saving unaltered backup copy of destination file %s1 in %s2"},
            {"LS_LOG_NCSFILEEXISTSKIP", "File \"%s1\" already exists in archive \"%s2\", file skipped..."},
            {"LS_LOG_NCSSAVEINERFORRIM", "Adding script \"%s1\" to archive \"%s2\"..."},
            {"LS_LOG_NCSCOMPILEDNOTFOUND", "Unable to find compiled version of file \"%s1\"! The compilation probably failed! Skipping..."},
            {"LS_LOG_NCSINCLUDEDETECTED", "Script \"%s1\" has no start function, assuming include file. Compile skipped..."},
            {"LS_LOG_NCSPROCNSSMISSING", "Unable to find processed version of file %s1; cannot compile it!"},
            {"LS_LOG_NCSSAVEERFRIM", "Saving changes to ERF/RIM file %s1..."},

            // SSF file handler
            {"LS_LOG_SSFNOMODIFIERS", "File \"%s1\" has no modifier section specified! Skipping it..."},
            {"LS_LOG_SSFFILENOTFOUND", "File %s1 could not be found! Skipping it..."},
            {"LS_LOG_SSFMODSTRREFS", "Modifying StrRefs in Soundset file \"%s1\"..."},
            {"LS_LOG_SSFSETTINGENTRY", "Setting Soundset entry \"%s1\" to %s2..."},
            {"LS_LOG_SSFINVALIDSTRREF", "Unable to set StrRef for entry \"%s1\", %s2 is not a valid StrRef value!"},
            {"LS_LOG_SSFUPDATESUMMARY", "Finished updating %s1 entries in file \"%s2\"."},
            {"LS_LOG_SSFEXCEPTIONERRORS", "%s1 [%s2] - file skipped!"},
            {"LS_LOG_SSFNOFILE", "No %s1 file was specified! Skipping it..."},

            // File handler
            {"LS_EXC_FHRENAMEFAILED", "Unable to locate source file \"%s1\" to rename to \"%s2\" and install, skipping..."},
            {"LS_EXC_FHNODESTPATHSET", "Error! No install path has been set!"},
            {"LS_EXC_FHNOSOURCEFILESET", "Error! No file to install is specified!"},
            {"LS_EXC_FHSOURCEDONTEXIST", "Error! File \"%s1\" set to be patched does not exist!"},
            {"LS_DLG_SELECTINSTALLFOLDER", "Please select the folder where your game is installed. (The folder containing the game executable.)"},
            {"LS_EXC_FHINVALIDGAMEFOLDER", "Invalid game directory specified!"},
            {"LS_EXC_FHTALKYMANNOTFOUND", "Invalid game folder specified, dialog.tlk file not found! Make sure you have selected the correct folder."},
            {"LS_LOG_FHINSTALLPATHSET", "Install path set to %s1."},
            {"LS_DLG_FILETYPETLK", "TLK file %s1"},
            {"LS_DLG_FILETYPE2DA", "2DA file %s1"},
            {"LS_DLG_FILETYPENSS", "NSS Script Source %s1"},
            {"LS_DLG_FILETYPESSF", "SSF Soundset file %s1"},
            {"LS_DLG_FILETYPEITM", "Item template %s1"},
            {"LS_DLG_FILETYPEUTC", "Creature template %s1"},
            {"LS_DLG_FILETYPEUTM", "Store template %s1"},
            {"LS_DLG_FILETYPEUTP", "Placeable template %s1"},
            {"LS_DLG_FILETYPEDLG", "Dialog file %s1"},
            {"LS_DLG_FILETYPEGFF", "GFF format file %s1"},
            {"LS_DLG_FILETYPEALL", "All files %s1"},
            {"LS_DLG_FILESELECTDESC", "Please select your %s1 file."},
            {"LS_DLG_FILESELECTDESCMOD", "Please select the %s1 file that came with this Mod."},
            {"LS_DLG_FILEWORD", "%s1 File"},
            {"LS_EXC_FHNODESTSELECTED", "Error! No valid game folder selected! Installation aborted."},
            {"LS_EXC_FHREQFILEMISSING", "Cannot locate required file %s1, unable to continue with install!"},
            {"LS_EXC_FHTLKFILEMISSING", "Error! Unable to locate TLK file to patch, \"%s1\" file not found!"},
            {"LS_LOG_FHDESTFILENOTFOUND", "Unable to locate archive \"%s1\" to modify or insert file \"%s2\" into, skipping..."},
            {"LS_LOG_FHDESTNOTFOUNDEXC", "Unable to load archive \"%s1\" to modify or insert file \"%s2\" into, skipping... (%s3)"},
            {"LS_LOG_FHCANNOTLOADDEST", "Unable to load archive \"%s1\" to insert file \"%s2\" into, skipping..."},
            {"LS_LOG_FHDESTRESEXISTMOD", "File \"%s1\" already exists in archive \"%s2\", modifying existing file..."},
            {"LS_LOG_FHSOURCENOTFOUND", "Unable to locate file \"%s1\" to rename to \"%s2\" and install, skipping..."},
            {"LS_LOG_FHADDTODEST", "Adding file \"%s1\" to archive \"%s2\"..."},
            {"LS_LOG_FHTEMPFILEFAILED", "Unable to make work copy of file \"%s1\". File not saved to ERF/RIM archive!"},
            {"LS_LOG_FHMAKEOVERRIDE", "No Override folder found, creating it at %s1."},
            {"LS_LOG_FHMISSINGARCHIVE", "Unable to locate archive \"%s1\" to insert script \"%s2\" into, skipping..."},
            {"LS_LOG_FHLOADARCHIVEEXC", "Unable to load archive \"%s1\" to insert script \"%s2\" into, skipping... (%s3)"},
            {"LS_LOG_FHLOADARCHIVEERR", "Unable to load archive \"%s1\" to insert script \"%s2\" into, skipping..."},
            {"LS_LOG_FHBACKUPSCRIPT", "Making backup copy of script file \"%s1\" found in override..."},
            {"LS_LOG_FHSCRIPTEXISTS", "Script file \"%s1\" already exists in override! Skipping..."},
            {"LS_LOG_FHUPDATEREPLACE", "Updating and replacing file %s1 in Override folder..."},
            {"LS_LOG_FHUPDATECOPY", "Updating and copying file %s1 to Override folder..."},
            {"LS_LOG_FHINSFILENOTFOUND", "Unable to locate file \"%s1\" to install, skipping..."},
            {"LS_LOG_FHCOPY2OVERRIDE", "Copying file %s1 to Override folder..."},
            {"LS_LOG_FHSAVEASSRCNOTFOUND", "Unable to locate file \"%s1\" to install as \"%s2\", skipping..."},
            {"LS_LOG_FHFILEEXISTSKIP", "A file named \"%s1\" already exists in the Override folder. Skipping..."},
            {"LS_LOG_FHNOTSLPATCHDATAFILE", "No file blueprint found in tslpatchdata folder, fallback to manual source..."},
            {"LS_DLG_MANUALLOCATEFILE", "File not found! Please locate the \"%s1\" (\"%s2\") file."},
            {"LS_LOG_FHCOPYFILEAS", "Copying file \"%s1\" as \"%s2\" to Override folder..."},
            {"LS_EXC_FHCRITFILEMISSING", "Critical error: Unable to locate file to patch, \"%s1\" file not found!"},
            {"LS_LOG_FHMODIFYINGFILE", "Modifying file \"%s1\" found in Override folder..."},

            {"LS_TLK_CHANGING", "Changing TLK entries..."},
            {"LS_TLK_NOIDEA", "Can't find Entry \"%s1\" to change..."},
            {"LS_TLK_CHANGNUM", "Changing Entry \"%s1\"..."},
            {"LS_TLK_CHANGETOTAL", "Changed \"%s1\" entries."},
            {"LS_TLK_DELETING", "Deleting TLK entries..."},
            {"LS_TLK_DELETENUM", "Deleting Entry \"%s1\"..."},
            {"LS_TLK_DELETETOTAL", "Deleted \"%s1\" entries."},

            {"LS_LOG_2DADELETINGROW", "Deleting row \"%s1\" in \"%s2\"..."},
            {"LS_LOG_2DADELETEROWERR", "Cannot find row \"%s1\" to delete!"},
            {"LS_LOG_2DADELETINGCOL", "Deleting column (\"%s1\") in \"%s2\"..."},
            {"LS_LOG_2DADELETECOLERR", "Cannot find column (\"%s1\" to delete!"},

            {"LS_GUI_UNINSTALLOPT", "Uninstall.ini file detected.\n\nDo you want to uninstall the mod?"}
        };

        public static string Format(string message, params string[] fixes)
        {
            int index;
            int scur = 1;
            int smax = fixes.Length;

            while (scur < smax + 1)
            {
                index = message.IndexOf("%s" + scur);

                if (index != -1)
                {
                    message = message.Substring(0, index) + fixes[scur - 1] + message.Substring(index + 3);
                }

                scur++;
            }

            return message;
        }

        public static void WriteInstallLog()
        {
            using (StreamWriter writer = new StreamWriter(Path.Combine(installPath, "installlog.rtf")))
            {
                writer.WriteLine("{\\rtf1\\ansi\\ansicpg1252\\deff0\\deflang1033{\\fonttbl{\\f0\\fnil\\fcharset0 Courier New;}}");
                writer.WriteLine("{\\colortbl ;\\red2\\green97\\blue17;\\red4\\green32\\blue152;\\red160\\green87\\blue2;\\red156\\green2\\blue2;\\red2\\green97\\blue17;}");
                writer.WriteLine("\\viewkind4\\uc1\\pard\\cf1\\b\\f0\\fs2");
                writer.WriteLine(logText);
                writer.WriteLine("\\b0 \\par }");
            }
        }

        public static void ProcessMessage(string message, int logLevel)
        {
            string color = "Black";
            string prefix = "";
            int? colorLog = null;

            if ((int)installInfoDict["iLoglevel"] == 0) { return; } // Off
            else if (logLevel == LOG_LEVEL_VERBOSE && (int)installInfoDict["iLoglevel"] == 4)
            {
                colorLog = 2;
                color = "Blue";
            } // Verbose
            else if (logLevel == LOG_LEVEL_ERROR && (int)installInfoDict["iLoglevel"] >= 1)
            {
                colorLog = 4;
                color = "Red";
                logErrors++;
                prefix = "Error: ";
            } // Error
            else if (logLevel == LOG_LEVEL_ALERT && (int)installInfoDict["iLoglevel"] >= 2)
            {
                colorLog = 3;
                color = "Orange";
                logAlerts++;
                prefix = "Warning: ";
            } // Alert
            else if (logLevel == LOG_LEVEL_INFORMATION)
            {
                colorLog = 0;
                color = "Black";
            } // Information
            else if (logLevel == LOG_LEVEL_NOTICE)
            {
                colorLog = 1;
                color = "Green";
            } // Notice

            if (logLevel == LOG_LEVEL_VERBOSE && color != "Blue")
            {
                return;
            }
            if (logLevel == LOG_LEVEL_ALERT && color != "Orange")
            {
                return;
            }
            if (logLevel == LOG_LEVEL_ERROR && color != "Red")
            {
                return;
            }

            if (color == "Green")
            {
                logTextDone = 2;
                if (logAlerts > 0 && logErrors == 0)
                {
                    colorLog = 3;
                    color = "Orange";
                }
                else if (logAlerts == 0 && logErrors > 0)
                {
                    colorLog = 4;
                    color = "Red";
                }
                else if (logAlerts > 0 && logErrors > 0)
                {
                    colorLog = 5;
                    color = "Mixed";
                }
            }

            string pre = "\\par ";
            if (logFirst)
            {
                pre = "";
                logFirst = false;
            }

            if (logTextDone == 0)
            {
                logText += $"{pre}\\b0 \\cf{colorLog}  \\bullet  {prefix}{message}\n";
                logTextDone = 1;
            }
            else if (logTextDone == 2)
            {
                logText += $"{pre}\\b\\cf{colorLog}  \\bullet  {prefix}{message}\n";
                logTextDone = 0;
            }
            else
            {
                logText += $"{pre}\\cf{colorLog}  \\bullet  {prefix}{message}\n";
            }

            if ((int)installInfoDict["bLogOld"] == 0)
            {
            }
            else
            {
            }

            double[] yviewData = null;

            if (yviewData[1] > 0.0)
            {
            }
        }
    }
}
