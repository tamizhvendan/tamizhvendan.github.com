---
layout: post
title: "Implementing Two-Factor Authentication in Suave"
date: 2016-12-19 18:34:27 +0530
comments: true
categories: 
  - fsharp
  - suave
---

Two-factor authentication is a type of [Multi-factor authentication](https://en.wikipedia.org/wiki/Multi-factor_authentication) which adds an extra layer of security to the appliactions. 

> A good example from everyday life is the withdrawing of money from a cash machine; only the correct combination of a bank card (something that the user possesses) and a PIN (personal identification number, something that the user knows) allows the transaction to be carried out. - [Wikipedia](https://en.wikipedia.org/wiki/Multi-factor_authentication)

[Google Authenticator](https://en.wikipedia.org/wiki/Google_Authenticator) is one of the popular application that implements two-factor authentication services. In this blog post we are going to learn how to implement Two-factor authentication in web appliactions developed using [suave](https://suave.io)

> Disclaimer: The idea presented here is a naive implemention of Two-factor authentication. The objective here is to demonstrate how to implement it in a functional programming language, F#. Things like SSL/HTTPS, preventing CSRF and other attacks are ignored for brevity. 

## Prerequiste

This blog post assumes that you are familier with the concept of Two-factor authentication and know how Google Authenticator works. 

If you are encountering this for the first time or less familier, then check out the below resources to get a picture of what is happening behind the scene.

* [Two-step verification](https://www.google.com/landing/2step) by Google 
* [How Google Authenticator works](https://garbagecollected.org/2014/09/14/how-google-authenticator-works/)
* [A Stack-Overflow Question](https://security.stackexchange.com/questions/35157/how-does-google-authenticator-work)

We are going to use [Time based One-time Password(TOTP)](https://en.wikipedia.org/wiki/Time-based_One-time_Password_Algorithm) algorithm in this blog post

## What we will be building

We are going to build a tiny web appliaction that has an inbuilt user account with the username `foo` and the password `bar`

{% img center border /images/suave_two_factor/Login.png 300 200 %}

After successful login the user will be redirected to the **Profile** page where the user sees his name with couple of buttons. One to enable *Two-factor authentication* and another one to *logout*

{% img center border /images/suave_two_factor/Profile.png 350 200 %}

Upon clicking the *Enable Two Factor Authentication* button, the user will be redirected to the **Enable Two Factor Authentication** page where the user has to scan the QR Code with the Google Authenticator App (For [Android](https://play.google.com/store/apps/details?id=com.google.android.apps.authenticator2&hl=en) or [iPhone](https://itunes.apple.com/in/app/google-authenticator/id388497605?mt=8)). Then he needs to enter the verification code to enable two factor authentication for his account. 

{% img center border /images/suave_two_factor/Enable_Two_Factor.png 450 250 %}

*Google Authenticator App*

{% img center border /images/suave_two_factor/Google_Authenticator.png 200 400 %}

If the verification code matches, the updated **Profile** page would look like 

{% img center border /images/suave_two_factor/Profile_After_Two_Factor.png 350 200 %}

Now if the user logout and login again, he will be prompted to enter the verification code 

{% img center border /images/suave_two_factor/Auth_Code_Prompt.png 250 150 %}

After entering the verification code from the Google Authenticator, the user will be redirected to his **Profile** page. 

## Getting Started

Create a new **F# Console Project** with then name *Suave.TwoFactorAuth* and use Paket to install the following dependencies. 

*paket.dependencies*

```
nuget FSharp.Core
nuget Suave
nuget DotLiquid
nuget Suave.DotLiquid
nuget OtpSharp
```

Then reference them in the *Suave.TwoFactorAuth* project.

*Suave.TwoFactorAuth/paket.references*

```
FSharp.Core
Suave
DotLiquid
Suave.DotLiquid
OtpSharp
```

The [OtpSharp](TODO) is a .NET library that we wil be using to generate keys and to verify the verification code from Google Authenticator app using the [TOTP](TODO) algorithm.

The reference to [DotLiquid](TODO) library is required to render the templates using [Suave.DotLiquid](TODO)


## Initializing DotLiquid

To use [DotLiquid](TODO) to render the views, we need to explicity set the templates directory. From this path, DotLiquid render the requested [liquid templates](TODO). 

```fsharp
// Suave.TwoFactorAuth/Suave.TwoFactorAuth.fs
module Suave.TwoFactorAuth.Main

open Suave
open System.IO
open System.Reflection
open Suave.DotLiquid

let initializeDotLiquid () =
  let currentDirectory =
    let mainExeFileInfo = 
      new FileInfo(Assembly.GetEntryAssembly().Location)
    mainExeFileInfo.Directory
  Path.Combine(currentDirectory.FullName, "views") 
  |> setTemplatesDir

[<EntryPoint>]
let main argv =  
  initializeDotLiquid ()
  0
```

As a convention we are going to create a directory *views*. This *views* directory going to have all the liquid templates of our appliaction

## Serving the Login Page

The first page that we are going to develop is the login page. Let's start from the views

The best practice to use liquid templates is to have a master template which provides the skeleton code with placeholders and the child templates fill the placeholders

Create a new directory with the name *views* in the *Suave.TwoFactorAuth* project and add a new liquid template *page.liquid*. This *page.liquid* is the master template for our application

{% img center border /images/suave_two_factor/page.png 300 300 %}

After creating change the 'Copy to output' property of the *page.liquid* file to 'Copy if newer' so that the view files are copied to the build output directory. This step is applicable for all the other view templates that we will be adding later

> If you are using VS Code or atom editor, you need to do this property change manually by opening the *Suave.TwoFactorAuth.fsproj* file 

```html
<!-- .... -->
<ItemGroup>
  <Folder Include="views\" />
</ItemGroup>
<ItemGroup>
  <None Include="views\page.liquid">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
<!-- .... -->
```

Then create a new template file *login.liquid* in the *views* directory 

{% img center border /images/suave_two_factor/login_view.png 500 300 %}

The *login.liquid* view extends the *page.liquid* view and fill the placeholders for `head` and `content`.

To display the error messages like *Password didn't match*, *login.liquid* view is bounded to the model of type `string`. 

Now we have the view template for the login page ready, the next step is to render it upon receiving a HTTP request.

Create a new fsharp source file *Login.fs* and update it as below

```fsharp
module Suave.TwoFactorAuth.Login

open Suave
open Suave.DotLiquid
open Suave.Filters
open Suave.Operators

let loginPath = "/login"

let renderLoginView (request : HttpRequest) =
  let errMsg =
    match request.["err"] with
    | Some msg -> msg
    | _ -> ""
  page "login.liquid" errMsg

let loginWebPart =
  path loginPath >=> choose [
      GET >=> request renderLoginView]
```

As a good practice let's create a new module `Web` which will be containing all the WebParts of the application

```fsharp
// Suave.TwoFactorAuth/Suave.Web.fs
open Suave
open Login

let app =   
  choose [
    loginWebPart]
```

Then start the webserver

```fsharp
// Suave.TwoFactorAuth/Suave.TwoFactorAuth.fs
// ...
open Web
// ...
let main argv =  
  // ...
  startWebServer defaultConfig app
  0 
```

## Handling User Login

## Enable Two-factor Authentication

## Login With Two-factor Authentication

## Summary
