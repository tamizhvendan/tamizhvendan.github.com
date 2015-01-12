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

Just a like any other build process, the first step is to clear build the directory. Fake follows the same convention of MsBuild's [Targets](http://msdn.microsoft.com/en-us/library/ms171462.aspx). Defining targets in Fake is less verbose compare to its MsBuild's counterpart. As a matter of fact, it is a fsharp function that take two parameters, the name of the target and a function which defines the target. 

Since it is a script file we need to add reference to the **FakeLib.dll** before writing the actual code.

```fsharp
#r "tools/FAKE/tools/FakeLib.dll"
open Fake

let buildDir = "./build"

Target "Clean" (fun _ -> CleanDir buildDir)

```

Less verbose, Isn't it ?

The ```CleanDir``` is a predefined function in Fake library which clean up this build directory. If the directory doesn't exist it creates the directory. 

### Build the solution

After cleaning the build directory the next step is to carrying out the actual build process. Before building the application we need to resolve the nuget packages being referred in the application. It's so simple in Fake. All you need to do is just call predefined function ```RestorePackages```.    

```fsharp
Target "Build" (fun _ ->

  RestorePackages()

  !! "./PhoneCat.sln"
    |> MSBuildRelease buildDir "Build"
    |> Log "Build-Output: "

)
```
The ```!!``` is a function which takes a file path and returns [FileIncludes](http://fsharp.github.io/FAKE/apidocs/fake-filesystem-fileincludes.html) which is Fake's internal representation of a file set.

FileIncludes [implements](https://github.com/fsharp/FAKE/blob/master/src/app/FakeLib/Globbing/FileSystem.fs#L95-l117) ```IEnumerable<string>``` so it can be used wherever we need an ```IEnumerable<string>```. 

The ```MsBuildRelease``` is also a predefined function in Fake which builds the given solution (which is piped from the ```!!```). The first two parameters are the output build directory path and the target names which should be run by MsBuild. Fake offers lot of functional wrappers on top of MsBuild and you can find more about it in this [api documentation.](http://fsharp.github.io/FAKE/apidocs/fake-msbuildhelper.html). 

The last line just pipes the log result from the ```MsBuildRelease``` function to the log output.

### Running unit tests using Nunit.Runner

Now we have built the solution and the next step is running the unit test cases. Since I've written the test cases using Nunit and we are going to see how to run nunit tests using Fake. Fake also supports [XUnit](http://fsharp.github.io/FAKE/apidocs/fake-xunithelper.html) and [MsTest](http://fsharp.github.io/FAKE/apidocs/fake-mstest.html). Porting the below code to other unit testing framework is very straight forward.

```fsharp
Target "Test" (fun _ ->
  !! (buildDir + "/*.Tests.dll")
    |> NUnit (fun p -> {p with ToolPath = "./tools/NUnit.Runners/tools" })
)
```
As we did it in the build target, the ```!!``` function picks all the unit test files (I've used a convention of naming the unit tests projects with the **.Tests** suffix) in the build directory and transform them to FileIncludes. 

The ```NUnit``` is yet another [inbuilt function of Fake](http://fsharp.github.io/FAKE/apidocs/fake-nunitsequential.html) which runs nunit on the given group of assemblies.

It takes two parameter, a function that returns the modified default ```NUnitParams``` value and a sequence of assembly paths. We have modified the default ```ToolPath``` property which specifies the file path of **NUnit Runner** 

As we did for Fake we have to modify the batch file as below to install the Nunit Runner nuget package before running the actual script.  

```bat
@echo off
cls
".nuget\NuGet.exe" "Install" "FAKE" "-OutputDirectory" "tools" "-ExcludeVersion"
".nuget\NuGet.exe" "Install" "NUnit.Runners" "-OutputDirectory" "tools" "-ExcludeVersion"

"tools\FAKE\tools\Fake.exe" build.fsx
pause
```
### Deploy the application in IIS

Well, now we are at the final step of the build process which deploy the application in the local IIS server. 

Deploying an application in IIS is super easy using Fake. All you need to do is just input necessary things like site name, virtual directory name, etc., and call the ```IIS``` function in **Fake.IIS** library which is installed as part of Fake.

To begin with we need to add reference in the beginning **build.fsx** to include the **Fake.IIS** library. 

```fsharp
#r "tools/FAKE/tools/Fake.IIS.dll"
```

This **Fake.IIS** makes heavy use of [ServerManager](http://msdn.microsoft.com/en-us/library/microsoft.web.administration.servermanager(v=vs.90).aspx) which helps to dynamically configure IIS 7.

```fsharp
Target "Deploy" (fun _ -> 
  
  let siteName = "fsharp"
  let appPool = "fsharp.appPool"
  let port = ":9999:"
  let vdir = "/phonecat"  
  let webBuildPath = buildDir + "/_PublishedWebsites/Web"
  let sitePhysicalPath = @"c:\inetpub\wwwroot\phonecat"
  
  XCopy webBuildPath sitePhysicalPath 
  
  (IIS
    (Site siteName "http" port @"C:\inetpub\wwwroot" appPool)
    (ApplicationPool appPool true "v4.0")
    (Some(Application vdir sitePhysicalPath)))
)
```

When we build a web project using MsBuild in release mode, it creates a deployable directory which can be accessed using the path */_PublishedWebsites/Web* in the build directory. So, after building the application we just need to copy this deployable directory to the **intepub\wwwroot** directory using the XCopy function in Fake.

The ```IIS``` function takes three parameters

* site - A function with the signature ```ServerManager -> Site``` which takes an instance of ServerManager and returns a [Site](http://msdn.microsoft.com/en-us/library/microsoft.web.administration.site(v=vs.90).aspx) with the given site settings. 
* appPool - A function with the signature ```ServerManager -> ApplicationPool``` which takes an instance of ServerManager and returns a [AppPool](http://msdn.microsoft.com/en-us/library/microsoft.web.administration.applicationpool(v=vs.90).aspx) with the given appPool settings.  
* app - An option type with the signature ```(Site -> ServerManager -> Application) option``` provides the [Application](http://msdn.microsoft.com/en-us/library/microsoft.web.administration.application(v=vs.90).aspx)  

Fake's IIS helper library has in-built function for Site, ApplicationPool and Application which takes the necessary inputs need for its creation and returns the associated instances. All of these function creates Site, ApplicationPool and Application respectively if they are not available in the IIS. 

### Running the Targets

In the above steps we just defined various Fake targets. These targets doesn't execute on its own and we need to explicitly define the order of its execution and initiate.

```fsharp
"Clean"
  ==> "Build"
  ==> "Test"
  ==> "Deploy"

RunTargetOrDefault "Deploy"
```

The ```==>``` is one of [the DSL in Fake](http://fsharp.github.io/FAKE/apidocs/fake-additionalsyntax.html) which is actually a function that defines the target execution order.

The ```RunTargetOrDefault``` function runs the target specified in the parameter or executes the default target.


### Summary

Fake is just awesome. We can automate so much thing with very few lines of code and it can be used in **any .net projects**. If you are using MsBuild Xml scripts, do try Fake! It will make you super productive. You can find the source of this step in the fsharp-phonecat repository. 

