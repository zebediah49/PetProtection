#!/bin/bash

dotnet build

TMPDIR=zip
mkdir -p "$TMPDIR"

cp Assemblies/PetProtection.dll "$TMPDIR"
cp icon.png manifest.json README.md "$TMPDIR"

rm -f PetProtection.zip
( cd "$TMPDIR";
zip -r ../PetProtection.zip *
)

rm -r "$TMPDIR"
