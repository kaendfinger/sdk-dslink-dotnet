#!/usr/bin/env bash
nuget install NUnit.Console -Version 3.0.1 -OutputDirectory packages
exit $(mono packages/NUnit.Console.3.0.1/tools/nunit3-console.exe UnitTests/bin/Release/UnitTests.dll)
