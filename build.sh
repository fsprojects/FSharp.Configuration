#!/bin/bash
if test "$OS" = "Windows_NT"
then
  cmd /C build.cmd
else
  dotnet tool restore
  dotnet paket restore
  mono packages/build/FAKE/tools/FAKE.exe build.fsx $@
fi
