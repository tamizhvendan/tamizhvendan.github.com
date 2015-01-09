---
layout: post
title: "Step-4 Build automation using FAKE"
date: 2015-01-06 05:22:32 +0530
comments: true
categories: 
  - fsharp
  - FAKE
  - DevOps 
---

> This is step 4 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In the last three steps, we have seen how to build an web application in fsharp by leveraging [Web-Api]({% post_url 2014-12-17-phonecat-backend-using-web-api-and-typeproviders %}), [Razor]({% post_url 2014-12-23-step-2-fsharp-phonecat-views-using-razor %}) and [SignalR]({% post_url 2015-01-02-step-3-phonecat-recommendation-system-using-f-number-agents %}). In this step for a change we are going to take the [DevOps](http://en.wikipedia.org/wiki/DevOps) side of the application and going to add the build automation capabilities for the application. Let's get stated!

## Fake - An awesome build automation library

[Fake](http://fsharp.github.io/FAKE/) is a F# version of very successful and widely used build automation libraries [Make](http://www.gnu.org/software/make/) and [Rake](http://en.wikipedia.org/wiki/Rake_%28software%29). The combination of [fsharp's expressiveness](http://fsharp.org/testimonials/) and the [Fake's DSL](http://fsharp.github.io/FAKE/gettingstarted.html) is a visual treat to the eyes and especially if you are someone who is using [MsBuild](http://en.wikipedia.org/wiki/MSBuild) xml files for doing build automation you will definitely appreciate it.


### Adding nuget.exe to .nuget folder

As we are going to drive the nuget packages installation from the fsharp script file, we will be needing a [command line version of nuget](nuget.org/nuget.exe). Download it and place it inside the **.nuget** folder

Also its a good practice to add a [Nuget.config file](http://docs.nuget.org/docs/reference/nuget-config-settings). Add it too under **.nuget** folder. 

```xml 
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://www.nuget.org/api/v2/" />
  </packageSources> 
</configuration>
```

### Adding build.bat file

The **build.bat** is a bootstrap file which initiates the entire build process. It's a typical fake build batch file. Add it in the root folder of the solution.

```bat
@echo off
cls
".nuget\NuGet.exe" "Install" "FAKE" "-OutputDirectory" "tools" "-ExcludeVersion"
"tools\FAKE\tools\Fake.exe" build.fsx
pause
``` 
It just download the Fake nuget package using the nuget.exe that we added earlier and call the fake.exe with the fsharp build script. This **build.fsx** script is not added yet. So, let's add them.

### Adding build.fsx file

Create a [fsharp script file](http://blogs.msdn.com/b/chrsmith/archive/2008/09/12/scripting-in-f.aspx) with the name **build.fsx** in the root directory. This script is going to implement all our build automation tasks by using fake library.


### Cleaning the build directory

### Build the solution

### Running unit tests using Nunit.Runner

### Deploy the application in IIS