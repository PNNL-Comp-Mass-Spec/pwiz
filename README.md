
![ProteoWizard Logo](http://proteowizard.sourceforge.net/images/pwiz_purple_logo.png "ProteoWizard")

The ProteoWizard Library and Tools are a set of modular and extensible open-source, cross-platform tools and software libraries that facilitate proteomics data analysis.

The libraries enable rapid tool creation by providing a robust, pluggable development framework that simplifies and unifies data file access, and performs standard chemistry and LCMS dataset computations.

Core code and libraries are under the Apache open source license; the vendor libraries fall under various vendor-specific licenses.

## Features
* reference implementation of HUPO-PSI mzML standard mass spectrometry data format
* supports HUPO-PSI mzIdentML 1.1 standard mass spectrometry analysis format
* supports reading directly from many vendor raw data formats (on Windows)
* modern C++ techniques and design principles
* cross-platform with native compilers (MSVC on Windows, gcc on Linux, darwin on OSX)
* modular design, for testability and extensibility
* framework for rapid development of data analysis tools
* open source license suitable for both academic and commercial projects (Apache v2)

## Official build status
------- | ----------------
Windows | [![Windows status](https://img.shields.io/teamcity/http/teamcity.labkey.org/s/bt83.svg?label=VS%202013)]
Linux   | [![Linux status](https://img.shields.io/teamcity/http/teamcity.labkey.org/s/bt17.svg?label=GCC%204.9)]
        
### Unofficial toolsets
OS      | Toolset | Status
------- | ----------------
Windows | VS2013  | [![VS2013 status](https://img.shields.io/appveyor/ci/chambm/pwiz.svg)] |
Linux   | GCC 5   | [![GCC5 status](https://travis-matrix-badges.herokuapp.com/repos/ProteoWizard/pwiz/branches/master/2)]
Linux   | GCC 6   | [![GCC6 status](https://travis-matrix-badges.herokuapp.com/repos/ProteoWizard/pwiz/branches/master/3)]
Linux   | GCC 7   | [![GCC7 status](https://travis-matrix-badges.herokuapp.com/repos/ProteoWizard/pwiz/branches/master/4)]
OS X    | GCC 4.9 | [![OSX status](https://travis-matrix-badges.herokuapp.com/repos/ProteoWizard/pwiz/branches/master/9)]
