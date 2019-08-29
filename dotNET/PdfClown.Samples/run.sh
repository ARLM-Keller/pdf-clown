#!/bin/bash
#
# Shell script to run PDF Clown samples on Mono.

(
# Insert a look-up reference to the PdfClown library directory!
export MONO_PATH=MONO_PATH:`pwd`/../PdfClown/build/package:`pwd`/../PdfClown/lib
cp PdfClownCLISamples.exe.config build/package
# Execute the test!
mono --debug ./build/package/PdfClownCLISamples.exe #2> ../log/PdfClownCLISamples.exe.log
)
