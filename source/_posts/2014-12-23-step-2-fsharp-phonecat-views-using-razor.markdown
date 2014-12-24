---
layout: post
title: "Step-2 Fsharp-PhoneCat Views using Razor"
date: 2014-12-23 19:47:20 +0530
comments: true
categories: 
  - fsharp
  - functional-programming
  - ASP.NET MVC
  - web-api
---
> This is step 2 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In the [last blog post]({% post_url 2014-12-17-phonecat-backend-using-web-api-and-typeproviders %}) we have created web-api endpoints which serve the data for the home page and in this blog post we are going to create views of the phones and manufacturers using [Razor](http://en.wikipedia.org/wiki/ASP.NET_Razor_view_engine).

### Phone View
{% img /images/fsharp_phonecat/step_2/phone.png %}

As we did it in the step-1 we are going to start with the controller which serves the phone view. Let's get started by creating a source file in *Web* project under the folders **Controllers** with the name ```PhoneController```

```fsharp
type PhoneController(phones : seq<Phone>) =
  inherit Controller()
  member this.Show (id : string) = 
    let phone = phones |> Seq.find (fun p -> p.Id = id) |> PhoneViewModel.ToPhoneViewModel 
    this.View(phone)
```

It just a straight forward code which just picks the phone with the given id, transform the selected phone to a view model and return the view.

The ```phones``` data which is being passed to the controller is actually a domain model representing the data that are required to show in the UI. Let's create it in the *Domain* project. Create a source file in the *Domain* project and name it as ```Catalog```.

```fsharp
namespace PhoneCat.Domain

open PhoneCat.Domain.Measures

module Catalog =    

  type Display = {
    ScreenResolution : string
    ScreenSize : float<inch>
    TouchScreen : bool
  }
    
  type Storage = {
    Flash : float<MB>
    Ram : float<MB>
  }

  type Android = {
    OS : string
    UI : string
  }

  type Camera = {
    Features : seq<string>
    Primary : string
  }

  type Phone = {
    Id : string
    Name : string
    Description : string
    Android : Android
    Camera : Camera
    Display : Display
    Weight : float<g>
    Storage : Storage
    Images : seq<string>
  }
```
It's a typical type definition in fsharp which describes the domain model ```Phone```. Also if you remember we have already created a type with the same name ```Phone``` in [the previous step](https://github.com/tamizhvendan/fsharp-phonecat/blob/1/Domain/Production.fs#L29-L33). Though they share the name both are being used in two different context. One for showing all the details of a phone and other one is for showing only few details about a phone. 

This is what we call us [bounded context in DDD](http://martinfowler.com/bliki/BoundedContext.html) which we can easily achieved in fsharp using modules.

We have also used another awesome feature of fsharp called [units-of-measure](http://fsharpforfunandprofit.com/posts/units-of-measure/) which help us [avoid failures in unit-coversion](http://en.wikipedia.org/wiki/Mars_Climate_Orbiter#Cause_of_failure) by providing strong typed data.

In the later posts we will be extending the domain based on theses measure types right now it just expresses the domain correct. These measure types are not created yet so lets create them adding a source file in *Domain* project with the name ```Measures```

```fsharp
namespace PhoneCat.Domain

module Measures = 
  [<Measure>] type inch
  
  [<Measure>] type g
  
  [<Measure>] type MB
  
  let private toUOM (measureString : string) stringToReplace (uom : float<_>) = 
    let value = 
      if measureString = "" then 0.
      else measureString.Replace(stringToReplace, "") |> float
    value |> ((*) uom)
  
  let toInch (inchStr : string) = toUOM inchStr "inches" 1.0<inch>
  let toGram (weightStr : string) = toUOM weightStr "grams" 1.0<g>
  let toMB (storageStr : string) = toUOM storageStr "MB" 1.0<MB>
```

We have defined types to represents measures in inch, gram and megabytes. We have also added few utility methods to translate the raw string to strongly typed data which does the follwoing

```text
"200.5inches" to 200.5<inch>
"1200MB" to 1200.<MB>
```
With the domain model in place the next step is to populate this domain model from the data-store. To do it we are going to reuse the PhoneTypeProvider that we have [created in the step-1](https://github.com/tamizhvendan/fsharp-phonecat/blob/1/DataAccess/TypeProviders.fs#L25). Since it has all the properties that we needed, we just need to add a function which does the mapping.

Open ```TypeProviders``` and add the below function

```fsharp
let ToCatalogPhone(phone : PhoneTypeProvider.Root) = 
  let android = { OS = phone.Android.Os; UI = phone.Android.Ui }
      
  let camera = { Features = phone.Camera.Features; Primary = phone.Camera.Primary }        
      
  let display = 
    { ScreenResolution = phone.Display.ScreenResolution
      ScreenSize = toInch phone.Display.ScreenSize
      TouchScreen = phone.Display.TouchScreen }

  let storage = { 
    Flash = toMB phone.Storage.Flash
    Ram = toMB phone.Storage.Ram}

  { Id = phone.Id
    Name = phone.Name
    Description = phone.Description
    Android = android
    Camera = camera
    Display = display
    Weight = toGram phone.SizeAndWeight.Weight
    Storage = storage 
    Images = phone.Images}
```
We are leveraging the utility functions ```toGram```, ```toMB``` defined above as part of measure types and do seamless mapping of domain model from mapping store.

The next step is wiring the controller with the domain model and the data-store. We are going to follow the same [Composition Root] design as we [did in step-1](https://github.com/tamizhvendan/fsharp-phonecat/blob/1/Web/Infrastructure.fs#L19-38)

Add a source file in the *Web* project with the name ```MvcInfrastructure``` and add the following code

```fsharp
namespace PhoneCat.Web

open PhoneCat.DataAccess
open PhoneCat.Domain
open PhoneCat.Web.Controllers
open System
open System.Web.Mvc

module MvcInfrastructure = 
  type CompositionRoot(phones : seq<PhoneTypeProvider.Root>) = 
    inherit DefaultControllerFactory() with
      override this.GetControllerInstance(requestContext, controllerType) = 
        if controllerType = typeof<HomeController> then 
          let homeController = new HomeController()
          homeController :> IController
        else if controllerType = typeof<PhoneController> then
          let phones' = phones |> Seq.map TypeProviders.ToCatalogPhone
          let phoneController = new PhoneController(phones')
          phoneController :> IController
        else
          raise <| ArgumentException((sprintf "Unknown controller type requested: %A" controllerType))
```

Similar to step-1 we are creating the composition root by passing the phones data and doing the wiring of different components.

Since we have created our custom ControllerFactory, we need to update the MVC configuration to use it when the framework creates the controllers.

Update the ```Global.asax.fs`` file as below

```fsharp
member x.Application_Start() =
  let phones = GitHubRepository.getPhones()
  AreaRegistration.RegisterAllAreas()
  GlobalConfiguration.Configure(new Action<HttpConfiguration>(fun config -> Global.RegisterWebApi(config, phones)))
  Global.RegisterFilters(GlobalFilters.Filters)
  Global.RegisterRoutes(RouteTable.Routes)
  ControllerBuilder.Current.SetControllerFactory(MvcInfrastructure.CompositionRoot(phones))
  BundleConfig.RegisterBundles BundleTable.Bundles
```
In the last step the we have retrieved the phones data [inside the RegisterWebApi method](https://github.com/tamizhvendan/fsharp-phonecat/blob/1/Web/Global.asax.fs#L65) and in this step we have moved it outside so that it can be used by both MVC Controllers and Web-Api controllers.

We have also changed the signature of RegisterWebApi method. In the [earlier step](https://github.com/tamizhvendan/fsharp-phonecat/blob/1/Web/Global.asax.fs#L51) it gets the configuration parameter alone and now it has two parameters ```httpConfiguration``` and ```phones```

The next pending task in Phone View is translating the Phone Domain model to Phone View Model. Add the Phone View model in the ```PhoneController``` created before and update it us below

```fsharp
type PhoneViewModel = 
  {
    Name : string
    Description : string
    Os : string
    Ui : string
    Flash : string
    Ram : string
    Weight : string
    ScreenResolution : string
    ScreenSize : string
    TouchScreen : string
    Primary : string
    Features : seq<string>
    Images : seq<string>    
  }

  with static member ToPhoneViewModel(phone : Phone) =
    let roundToDigits (value:float) = String.Format("{0:0.00}", value)
    let concatWithSpace str2 str1 = str1 + " " + str2
    let uomToString (measureValue: float<_>) measureName =
      measureValue |> float |> roundToDigits |> concatWithSpace measureName
    
    { 
      Name = phone.Name
      Description = phone.Description
      Os = phone.Android.OS
      Ui = phone.Android.UI
      Flash = phone.Storage.Flash |> int |> sprintf "%d MB"
      Ram =  phone.Storage.Ram |> int |> sprintf "%d MB"
      Weight = uomToString phone.Weight "grams"
      ScreenResolution = phone.Display.ScreenResolution
      ScreenSize = uomToString phone.Display.ScreenSize "inches"
      TouchScreen = if phone.Display.TouchScreen then "Yes" else "No"
      Primary = phone.Camera.Primary
      Features = phone.Camera.Features
      Images = phone.Images
    }
``` 
With all in place, we just need to create a razor view with the name ```Show.cshtml``` inside the **View** -> **Phone** directory of *Web* project and update it [as mentioned here.](https://github.com/tamizhvendan/fsharp-phonecat/blob/2/Web/Views/Phone/Show.cshtml) 

That's it. Phone View is up and running! Click the Phone Links in the Home Page it will take you to the Phones View Page.

### Manufacturers View
{% img /images/fsharp_phonecat/step_2/manufacturer.png %}
Manufatures View is follows the similar steps that we have used to create the Phone View. It displays the Phones manufatured by a selected manufaturer in the home page.

Let's start from the controller. Create a controller in the *Web* project with the name ```ManufacturerController```

```fsharp
namespace PhoneCat.Web.Controllers

open System.Web
open System.Web.Mvc
open PhoneCat.Domain
open System

type ManufacturerViewModel = {
  Name : string
  Phones : seq<Phone>
}
with static member ToManufacturerViewModel (name, phones) =
      {Name = ManufacturerName.ToString name; Phones = phones}

type ManufacturerController 
  (
    getPhones : ManufacturerName -> (ManufacturerName * seq<Phone>)
  ) =
  inherit Controller()
  
  member this.Show (id : string) = 
    
    let viewModel = 
      id 
      |> ManufacturerName.ToManufacturerName
      |> getPhones
      |> ManufacturerViewModel.ToManufacturerViewModel

    this.View(viewModel)
```

The ```ManufacturerController``` depends on the function ```getPhones``` which gives the Phones manufactured by the given manufacturer. The action method ```Show``` converts given id to the manufaturer name,  get the Phones, convert them to ```ManufacturerViewModel``` and render the view.

The domain model ```Phone``` mentioned here is the one that we have [already created in step-1](https://github.com/tamizhvendan/fsharp-phonecat/blob/1/Domain/Production.fs#L29-L33). The ```getPhones```` domain function is yet to be created. 

Open ```Phones``` file in the *Domain* project and add the ```getPhonesOfManufacturer``` function.

```fsharp
let getPhonesOfManufacturer (phones : seq<Phone>) (manufacturerName) =
    let phones' = 
      phones
      |> Seq.filter (fun p -> ManufacturerName.ToManufacturerName p.Name = manufacturerName)
    (manufacturerName, phones')
```
The ```getPhonesOfManufacturer``` function takes a sequence of phones and a manufacturer name, filters them for the given manufacturer and return a [tuple](http://fsharpforfunandprofit.com/posts/tuples/) that contain the manufacturer name and the sequence of phones.

As we have already created the data-store functions which serves the required data, we just need to wire up the controller.

Update ```MvcInfrastructure``` file in the *Web* project to create ```ManufacturerController```

```fsharp
module MvcInfrastructure = 
  type CompositionRoot(phones : seq<PhoneTypeProvider.Root>) = 
    inherit DefaultControllerFactory() with
      override this.GetControllerInstance(requestContext, controllerType) = 
        // ... Existing code ignored for brevity 
        else if controllerType = typeof<ManufacturerController> then
          let getPhonesByManufactuerName = phones |> Seq.map TypeProviders.ToPhone |> Phones.getPhonesOfManufacturer
          let manufacturerController = new ManufacturerController(getPhonesByManufactuerName)
          manufacturerController :> IController
        else
          raise <| ArgumentException((sprintf "Unknown controller type requested: %A" controllerType))
``` 

Thanks to the [partial function](http://fsharpforfunandprofit.com/posts/partial-application/) we have partially applied the first parameter alone for the ```Phones.getPhonesOfManufacturer``` function which has the signature ```seq<Phone> -> ManufacturerName -> seq<Phone>``` and created a new function on the fly with the signature ```ManufacturerName -> seq<Phone>``` which is the exactly the function that is needed by the ```ManufacturerController```

The final step is to create a razor view with the name ```Show.cshtml``` inside the **View** -> **Manufacturer** directory of *Web* project and update it [as mentioned here.](https://github.com/tamizhvendan/fsharp-phonecat/blob/2/Web/Views/Manufacturer/Show.cshtml) 