#!/bin/bash

mono .paket/paket.exe restore
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi

mono packages/build/FAKE/tools/FAKE.exe build.fsx $@
