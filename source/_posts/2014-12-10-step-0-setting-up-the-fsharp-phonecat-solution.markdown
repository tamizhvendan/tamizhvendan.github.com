---
layout: post
title: "Step 0 - Setting up the fsharp-phonecat Solution"
date: 2014-12-10 22:15:50 +0530
comments: true
categories: 
  - fsharp
  - functional-programming
  - ASP.NET MVC
---

> This is step 0 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In this blog post, we are going to set up the visual studio solution and create the high level project structure for the *fsharp-phonecat* application. 

**Let's get started!**

#### Setting Up Visual Studio Solution

The first step is to create an blank visual studio solution and name it as **PhoneCat**

{% img center /images/fsharp_phonecat/step_0/BlankSolution.png 700 700 %}

After creating the blank solution create two fsharp class libraries and name it as **Domain** and **DataAccess** respectively.

{% img center /images/fsharp_phonecat/step_0/ClassLibrary.png 700 700 %}

Do delete the sample fsharp files Domain1.fs, DataAccess1.fs and Script.fsx that are created by default while creating the class libraries

Then create a ASP.NET MVC web project using [F# MVC 5 template](https://visualstudiogallery.msdn.microsoft.com/39ae8dec-d11a-4ac9-974e-be0fdadec71b) and name it as *Web*

{% img center /images/fsharp_phonecat/step_0/WebProject.png 700 700 %}

{% img center /images/fsharp_phonecat/step_0/MVC5.png %}

We are going to use TDD, so create test projects for each of the above projects created and name it as **Domain.Tests**, **DataAccess.Tests** and **Web.Tests** respectively.

The final project structure would look like

{% img center /images/fsharp_phonecat/step_0/ProjectStructure.png %}

F# MVC 5 template comes with a default Bootstrap theme and we are going to extend it by using e-commerce theme from [Start Bootstrap](http://startbootstrap.com/template-overviews/shop-homepage/). Download the theme and add *shop-homepage.css* to the Content directory of **Web** project. After adding it update *Global.asax.fs* to include this css file while bundling.

``` fsharp BundleConfig after adding shop-homepage.css
type BundleConfig() =
    static member RegisterBundles (bundles:BundleCollection) =
        bundles.Add(ScriptBundle("~/bundles/jquery")
            .Include([|"~/Scripts/jquery-{version}.js"|]))

        bundles.Add(ScriptBundle("~/bundles/modernizr")
            .Include([|"~/Scripts/modernizr-*"|]))

        bundles.Add(ScriptBundle("~/bundles/bootstrap")
            .Include("~/Scripts/bootstrap.js", 
                        "~/Scripts/respond.js"))

        bundles.Add(StyleBundle("~/Content/css")
            .Include("~/Content/bootstrap.css",
                        "~/Content/site.css",
                        "~/Content/shop-homepage.css"))
``` 

Now the stage is set for awesomeness. Update **_Layout.cshtml** and **Index.cshtml** as mentioned in the bootstrap theme and run the web project.

{% img center /images/fsharp_phonecat/step_0/HomePage.png 800 800 %}

You can checkout the code [here](https://github.com/tamizhvendan/fsharp-phonecat/tree/0). In the [next-post]({% post_url 2014-12-17-phonecat-backend-using-web-api-and-typeproviders %}) we will be wiring up the backend. Stay tuned !