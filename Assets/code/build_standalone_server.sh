#!/bin/bash
SRC="server.cs standalone_server.cs network_utils.cs client.cs"
mkdir server 2> /dev/null
cp $SRC server
cp ../server_data server
cd server
mcs $@ -out:server -langversion:latest -define:STANDALONE_SERVER *cs
rm $SRC
