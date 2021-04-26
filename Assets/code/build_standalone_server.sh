#!/bin/bash

# Make sure we're here (where the script is)
cd $(dirname $0)

# Source files to compile/compiler options
SRC="server.cs standalone_server.cs network_utils.cs client.cs"
MCS="-out:server -langversion:latest -define:STANDALONE_SERVER"

# Useful for testing
MCS="$MCS -define:NO_SERVER_TIMEOUT"

# Make subdirectory to build the server in
mkdir server 2> /dev/null

# Copy source files/server data to the subdirectory
cp $SRC server
cp ../server_data server

# Move to subdirectory and compile the server
cd server
echo "mcs $@ $MCS $SRC"
mcs $@ $MCS $SRC

# Remove copied source files
rm $SRC
