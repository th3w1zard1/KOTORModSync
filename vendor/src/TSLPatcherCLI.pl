# TSLPatcherCLI as designed in Perl...
# Main script. Will use libraries TSLPatcher::FunctionsCLI.
###############################################################################

use lib 'lib/site'; # links the Bioware packages
use TSLPatcher::FunctionsCLI;

my $gamePath   		= $ARGV[0]; # swkotor directory
my $modPath 		= $ARGV[1]; # mod directory (folder where TSLPatcher lives)
my $installOption 	= $ARGV[2]; # Array index for mods with install options

# Change directory dividers to forward slash
$gamePath =~ s/\\/\//g;
$modPath =~ s/\\/\//g;
print "\n~~~ Game Path: $gamePath\n~~~ Mod Path: $modPath\n";

# Sets the base paths for the FunctionsCLI library
TSLPatcher::FunctionsCLI::Set_Base($modPath, $gamePath);

# With install options: Run ProcessNamespaces, SetInstallOption, RunInstallOption, Install
# Without install options: Run ProcessInstallPath, Install
if ($installOption eq "") {
	TSLPatcher::FunctionsCLI::ProcessInstallPath;
	TSLPatcher::FunctionsCLI::Install;
} else {
	TSLPatcher::FunctionsCLI::ProcessNamespaces;
	TSLPatcher::FunctionsCLI::SetInstallOption($installOption);
	TSLPatcher::FunctionsCLI::RunInstallOption;
	TSLPatcher::FunctionsCLI::Install;
}
