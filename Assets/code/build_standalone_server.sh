#!/bin/bash

# Make sure we're here (where the script is)
cd $(dirname $0)

# Get version info
long_hash=$(git rev-parse HEAD)
date=$(git show --no-patch --no-notes --pretty='%cd' --date=format:'%Y.%m.%d' $long_hash)
short_hash=$(git rev-parse --short HEAD)
version=$date.$short_hash

# Generate a cs source file with embedded version info
gs="gen_server_data.cs"
echo "static class gen_server_data" > $gs
echo "{" >> $gs
echo '    public static string version => "'$version'";' >> $gs
echo "}" >> $gs

# Source files to compile/compiler options
SRC="server.cs standalone_server.cs network_utils.cs client.cs $gs"
MCS="-out:server -langversion:latest -define:STANDALONE_SERVER"

# Useful for testing
MCS="$MCS -define:NO_SERVER_TIMEOUT"

# Make subdirectory to build the server in
mkdir server 2> /dev/null

# Copy source files/server data to the subdirectory
cp $SRC server
cp ../server_data server
rm $gs # Remove generated .cs file

# Move to subdirectory and compile the server
cd server
echo "mcs $@ $MCS $SRC"
mcs $@ $MCS $SRC

# Remove copied source files
rm $SRC
