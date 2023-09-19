#!/usr/bin/perl

# Find all the C# files
my $workingDirectory = "*.cs";
my @files = glob($workingDirectory);

print "Welcome to the ECS updater!\n";
print "This program will attempt to perform some of the tedious operations of updating to ECS 1.0.0 from 0.51.\n";

print "\n!!! THIS IS A PROTOTYPE; ALL FILES SHOULD BE BACKED UP BEFORE USE !!!\n\n";

print "Press ENTER to begin...\n";
readline;

# Create a changelog to log all the changes
my @changelog = ();

# Fix the [GenerateAuthoringComponent]
foreach (@files) {
    # Create file-specific variables
    my $lineNumber = 1;
    my $containsAuthoring = 0;
    my $unityEngineImport = 0;
    my @attributes = ();
    my $currentAttribute = "";

    # Store a copy of the file name without the file extention
    $rawName = substr $_, 0, -3;

    # Open the file to read all the data
    open(readFile, "<$_");

    @data = <readFile>;

    close(readFile);

    # Reopen the file, but truncate it and write to it
    open(writeFile, ">$_");
    
    # For each line of the file
    foreach(@data) {
        # Remove the newline character
        chomp($_);

        # If the file contains a [GenerateAuthoringComponent] command, remember that
        if ($_ =~ /\[GenerateAuthoringComponent\]/) {
            $containsAuthoring = 1;

            if ($unityEngineImport == 0) {
                print writeFile "using UnityEngine;\n\n";
            }
        }
        elsif ($_ =~ /using\sUnityEngine;/) {
            $unityEngineImport = 1;

            # Write back to the file
            print writeFile "$_\n";
        }
        else {
            # Write back to the file
            print writeFile "$_\n";

            if ($containsAuthoring == 1) {
                if ($_ =~ /(public\s\w+\s\w+;)/) {
                    push @attributes, $1;
                }

                elsif ($_ =~ /(public\s\w+\s(\w+,\s)+\w+;)/) {
                    push @attributes, $1;
                }

                elsif ($_ =~ /(public\s\w+\s(\w+,\s)*\w+,)/) {
                    $currentAttribute = "${currentAttribute}$1\n";
                }

                elsif ($_ =~ /((\w+,\s)*\w+,)/) {
                    $currentAttribute = "${currentAttribute}\t\t$1\n";
                }

                elsif ($_ =~ /((\w+,\s)*\w+;)/) {
                    $currentAttribute = "${currentAttribute}\t\t$1\n";

                    push @attributes, $currentAttribute;

                    $currentAttribute = "";
                }
            }
        }

        $lineNumber++;
    }

    # If it contains an authoring component, do extra stuff
    if ($containsAuthoring == 1) {
        push @changelog, "Line: $lineNumber\nA [GenerateAuthoringComponent] was detected in $_. The file was renamed ${rawName}Authoring.cs and a corresponding MonoBehaviour class was added at the indicated line.\n\n";

        print writeFile "\n";
        $lineNumber++;

        print writeFile "public class ${rawName}Authoring : MonoBehaviour\n";
        $lineNumber++;
        
        print writeFile "{\n";
        $lineNumber++;

        foreach (@attributes) {
            # Replace Entity objects with GameObject objects
            $_ =~ s/public\sEntity/public GameObject/;

            print writeFile "\t$_\n";
            $lineNumber++;
        }

        print writeFile "}";

        close(writeFile);

        rename "$_", "${rawName}Authoring.cs";
    }
    else {
        close(writeFile);
    }
}

foreach (@changelog) {
    print $_;
}