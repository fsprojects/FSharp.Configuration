#### 1.5.0 - 15.06.2018
* Tweak INI File Parser to allow semicolon characters inside string values (thanks [@zakakula](https://github.com/zakaluka)!).
  * NOTE: This will result in comments no longer being valid beside string values, instead comments should go on a line above the key-value pair

#### 1.4.0 - 23.02.2018
* Migration to latest TPSDK

#### 1.3.1 - 24.09.2017
* `SharpYaml.dll` added to NuGet package

#### 1.3.0 - 14.06.2017
* Explicit dependency on FSharp.Core
* Target .NET 4.5
* Target FSharp.Core 4.0.0.1

#### 1.2.0 - 20.05.2017
* FSharp.Core 4.4.1.0

#### 1.1.0 - 12.05.2017
* Mono 5 support

#### 1.0.1 - 22.04.2017
* NuGet package targets .NET 4.6

#### 1.0.0 - 12.04.2017
* YamlConfig provider: add `InferTypesFromStrings` optional parameter

#### 0.6.4 - 18.11.2016
* Add support for Guids in Yaml and AppSettings

#### 0.6.2 - 21.08.2016
* Handle int32 values loading from YAML files into int64 fields

#### 0.6.1 - 13.06.2016
* YamlConfigProvider: added support for having mixed type content fall back to strings

#### 0.6.0 - 16.05.2016
* YamlConfigProvider throws if file is not found or fails to load
* YamlConfigProvider allows keys of any type

#### 0.5.12 - 01.03.2016
* Added support for data in int64 to YamlConfigProvider

#### 0.5.11 - 24.01.2016
* Add Path.GetFullPath to the filePath calculation in YamlConfigProvider

#### 0.5.10 - 20.01.2016
* Yaml provider: added event to monitor errors in parsing from LoadAndWatch

#### 0.5.9 - 02.01.2016
* Yaml provider handle nested lists

#### 0.5.8 - 30.12.2015
* Get SharpYaml from NuGet but pack dll inside of FSharp.Configuration NuGet package
* Fix Travis build
* For Mono compatibility: TypeConstructor path should be able to be separated by both "/" and "\"
* AppSettingsProvider: Added possibility to change the application that is being configured.
* Fixed AppSettingsProvider to work in parallel with multiple configs.

#### 0.5.7 - 22.11.2015
* Revert dependency on SharpYaml

#### 0.5.6 - 22.12.2015
* Fixed: SharpYaml NuGet package was not listed as dependency of FSharp.Configuration one
* infer URLs with "tcp" schema

#### 0.5.5 - 20.12.2015
* Same as 0.5.4, NuGet publishing problems

#### 0.5.4 - 20.12.2015
* Support "file" and "ssh" uri schemas
* Use SharpYaml NuGet package

#### 0.5.3 - 12.06.2015
* AppConfig type provider raises better exception if a settings or connection string is missing at runtime.

#### 0.5.2 - 12.06.2015
* Add caching into all type providers

#### 0.5.1 - 18.03.2015
* fixed: YamlConfigProvider does not always parse floating point scalars

#### 0.5.0 - 09.03.2015
* YamlConfigProvider supports sequence of maps

#### 0.4.10 - 06.02.2015
* Better YamlConfigProvider error reporting in case of duplicated keys

#### 0.4.9 - 06.01.2015
* Fixed: AppSettings type provider does not read connection strings at runtime

#### 0.4.8 - 19.12.2014
* Use HostingEnvironment.IsHosted to determine that AppSettings type provider is used in a Web application

#### 0.4.7 - 12.12.2014
* Fixed design time memory leaks in all providers https://github.com/fsprojects/FSharp.Configuration/pull/48

#### 0.4.6 - 06.12.2014
* Fixed: AppSettingsProvider does not find web.config at runtime

#### 0.4.5 - 06.12.2014
* Fixed: AppSettingsProvider infers URLs as ints

#### 0.4.4 - 22.10.2014
* Add support for connection strings to AppSettings TP

#### 0.4.3 - 20.09.2014
* IniFile provider - https://github.com/fsprojects/FSharp.Configuration/pull/34

#### 0.3.0 - 19.09.2014
* Making config settings writable -https://github.com/fsprojects/FSharp.Configuration/pull/29
* Relax yaml tp loading rules - https://github.com/fsprojects/FSharp.Configuration/pull/32

#### 0.2.2 - 17.01.2013
* Allows to read bitmaps from resources

#### 0.2.1 - 17.01.2013
* Fixed bug with the AppSettings type provider and .fsx files

#### 0.2.0 - 15.01.2013
* Yaml type provider renamed to Yaml Config type provider

#### 0.1.0 - 15.01.2013
* First official release of ResX, Yaml and AppSettings type provider

#### 0.1.0-alpha5 - 15.01.2013
* Make it compatible with .NET 4.0

#### 0.1.0-alpha4 - 15.01.2013
* Using latest version of SharpYaml

#### 0.1.0-alpha3 - 15.01.2013
* Initial release of the Yaml type provider

#### 0.1.0-alpha2 - 14.01.2013
* Initial release of the ResX type provider

#### 0.1.0-alpha - 14.01.2013
* Initial release
