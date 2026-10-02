# Third-party notices

`src/Pica.Desktop/Services/FileAssociations/WindowsUserChoiceHash.cs` is a C#
adaptation of Mozilla's `browser/components/shell/WindowsUserChoice.cpp`.
`tests/Pica.Desktop.Tests/Services/FileAssociations/WindowsUserChoiceHashTests.cs`
uses Windows-generated reference vectors from Mozilla's
`toolkit/mozapps/defaultagent/tests/gtest/SetDefaultBrowserTest.cpp`.

These files are covered by the Mozilla Public License, version 2.0. The license
is included in `LICENSES/MPL-2.0.txt`. All other Pica files retain their existing
licenses. Source for the adaptations is distributed in the
[Pica repository](https://github.com/Krivodel/Pica).

Original source:

- https://github.com/mozilla/gecko-dev/blob/master/browser/components/shell/WindowsUserChoice.cpp
- https://github.com/mozilla/gecko-dev/blob/master/toolkit/mozapps/defaultagent/tests/gtest/SetDefaultBrowserTest.cpp
