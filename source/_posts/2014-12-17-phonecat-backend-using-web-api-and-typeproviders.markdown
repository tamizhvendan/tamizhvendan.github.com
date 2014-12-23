---
layout: post
title: "Step 1 - Phonecat backend using Web Api and TypeProviders"
date: 2014-12-17 17:31:44 +0530
comments: true
categories: 
  - fsharp
  - functional-programming
  - ASP.NET MVC
  - web-api
---

> This is step 1 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In the [last blog post]({% post_url 2014-12-10-step-0-setting-up-the-fsharp-phonecat-solution %}) we have created the basic home page of phone cat with the placeholders and in this post we are going it wire the home page with the backend web apis.

{% img /images/fsharp_phonecat/step_1/home_page.png %}

As the above screenshot indicates we are going to develop three web api endpoints which serve the data to the front-end and we are going to use the existing json data available in [angular-phonecat](https://github.com/angular/angular-phonecat/tree/master/app/phones) repository as our backend data store.

Initially I've planned to include TDD steps in this post but to keep it simple I am ignoring it. If you are interested in the tests that I've written check the [source code.](https://github.com/tamizhvendan/fsharp-phonecat/tree/1)

### Promotions Api

Let's start with the promotions api which serves the data of three recently launched mobile phones. The first step is to create an ApiController called "PromotionsController". You can create it by right clicking on the "Controllers" directory in the **Web** project and select **Add -> Source file**. In the dialog box, name it as "PromotionsController"

After creating, update the controller with the dependencies as mentioned below

``` fsharp
namespace Web.Controllers

open System.Web.Http
open PhoneCat.Domain

[<RoutePrefix("api/promotions")>]
type PromotionsController
  (
    getPromotions : seq<PhoneIndex> -> seq<PromotionPhone>,
    phoneIndexes: seq<PhoneIndex>
  ) =
  inherit ApiController()
```

The ```PromotionsController``` has two dependencies. 

* ```getPromotions``` is a function a that takes a sequence of a domain model ```PhoneIndex``` and returns the sequence of domain model ```PromotionPhone``` which represents the phones that are recently launched.

* ```phoneIndexes``` is a sequence of domain model ```PhoneIndex``` that represents the phones available in the backend data store.

These domain models currently not exists, so add them in the domain project as mentioned below

Right click on the domain project and select **Add -> Source file**. In the dialog box, name it as "Promotions". It creates a fsharp module with the name ```Promotions``` and add the ```PromotionPhone``` domain model

```fsharp
namespace PhoneCat.Domain
[<AutoOpen>]
module Promotions = 

  type PromotionPhone = {
    Id : string
    ImageUrl : string
    Name : string
  }
```
Similarly, create an another source file called ```Production``` and add the domain model ```PhoneIndex```

```fsharp
namespace PhoneCat.Domain

[<AutoOpen>]
module Production = 

  type PhoneIndex =
    { Id : string
      Name : string
      ImageUrl : string
      Age : int }
```

The ```Age``` property represents the age of the phone since its launch (so lower is younger!). Now the domain models are ready, the next step is to create an end point to expose it. 

Let's add a action method in PromotionsController which does the same

```fsharp
  [<Route("")>]
  member this.Get () = 
    getPromotions phoneIndexes
``` 

The scaffolding is ready and the next step is to add the domain method ```getPromotions``` which does the actual business logic of giving recently launched phones.

Add the ```getPromotions``` function in the ```Promotions``` file as below

```fsharp

let getPromotions (phoneIndexes : seq<PhoneIndex>) = 
  phoneIndexes 
  |> Seq.filter (fun phone -> phone.Age < 3) 
  |> Seq.map (fun phone -> {Id = phone.Id; Name = phone.Name; ImageUrl = phone.ImageUrl})

```

The ```getPromotions``` just filters the phones with the age less than 3 and map them to the corresponding ```PromotionPhone``` types.

Now its time to get the actual data from the data store. As I have mentioned earlier we will be using [angular-phonecat](https://github.com/angular/angular-phonecat/tree/master/app/phones) repository as our data store. To access the data we will be using [JsonTypeProvider](http://fsharp.github.io/FSharp.Data/library/JsonProvider.html).

In the *DataAccess* project install the nuget package [FSharp.Data](https://www.nuget.org/packages/FSharp.Data) and add a source file ```TypeProviders```. Then update it with the type provider for Phone Index

``` fsharp
namespace PhoneCat.DataAccess
open FSharp.Data
open PhoneCat.Domain

[<AutoOpen>]
module TypeProviders =

  [<Literal>]
  let private samplePhoneIndexes = """
  [{
      "age": 1,
      "id": "id0",
      "imageUrl": "id0.jpg",
      "name": "Name0",
      "snippet": "Sample Snippet0"
  },{
      "age": 2,
      "id": "id1",
      "imageUrl": "id1.jpg",
      "name": "Name1",
      "snippet": "Sample Snippet2"
  }]
  """
  
  type PhoneIndexTypeProvider = JsonProvider<samplePhoneIndexes>

  let ToPhoneIndex (phoneIndex : PhoneIndexTypeProvider.Root) =
    { Id = phoneIndex.Id; Name = phoneIndex.Name; Age = phoneIndex.Age; ImageUrl = phoneIndex.ImageUrl }

```

The ```ToPhoneIndex``` is a utility function which converts the data store model ```PhoneIndexTypeProvider.Root``` to the domain model ```PhoneIndex```

Now we got the type provide for ```PhoneIndex```, the next step is populating it from the github repository. To do that add a source file ```GitHubRepository``` and update it as below

```fsharp

namespace PhoneCat.DataAccess
open PhoneCat.Domain

module GitHubRepository = 

  [<Literal>]
  let private gitHubRepoUrl = "https://raw.githubusercontent.com/angular/angular-phonecat/master/app/phones/"
  
  let private phoneIndexes = PhoneIndexTypeProvider.Load(gitHubRepoUrl + "phones.json")

  let getPhoneIndexes () = phoneIndexes

```

That's it! just three line of code to get the data from the [angular-phonecat repo](https://raw.githubusercontent.com/angular/angular-phonecat/master/app/phones/phones.json) and map to the equivalent strongly typed models  

#### Composition Root

Now we need to wire up the controller with the domain and the data access. We can use a [Dependency Injection Container](http://www.hanselman.com/blog/ListOfNETDependencyInjectionContainersIOC.aspx) to achieve it. In fact it used to be my default decision. But after reading this [wonderful blog post](http://blog.ploeh.dk/2012/11/06/WhentouseaDIContainer/) by [Mark Seeman](https://twitter.com/ploeh) I have actually started rethinking about dependency injection. In this sample application we are going to use the [Composition Root](http://blog.ploeh.dk/2011/07/28/CompositionRoot/) as suggested by Mark Seeman.

In the *Web* project create a source file with the name ```Infrastructure``` and add the following composition root

```fsharp
namespace PhoneCat.Web

open System
open System.Web.Http.Dispatcher
open System.Web.Http.Controllers
open Web.Controllers
open PhoneCat.DataAccess
open PhoneCat.Domain

module Infrastructure =       
    
  type CompositionRoot 
    (
      phoneIndexes : seq<PhoneIndexTypeProvider.Root>  
    ) =                
    interface IHttpControllerActivator with           

      member this.Create(request, controllerDescriptor, controllerType) =
          
        let phoneIndexes' = phoneIndexes |> Seq.map TypeProviders.ToPhoneIndex

        if controllerType = typeof<PromotionsController> then
            let promotionsController = new PromotionsController(getPromotions, phoneIndexes')
            promotionsController :> IHttpController
        else
            raise <| ArgumentException((sprintf "Unknown controller type requested: %A" controllerType))
```

We have actually created a custom implementation of ```IHttpControllerActivator``` as mentioned [here](http://blog.ploeh.dk/2012/09/28/DependencyInjectionandLifetimeManagementwithASP.NETWebAPI/) which takes care of initializing the controllers by wiring the data store and the domain functions with the controller

The only pending task is tell ASP.NET Web Api to use this composition root. Update the *Global.asax.fs* file as below

```fsharp
static member RegisterWebApi(config: HttpConfiguration) =
       
  // existing configuration will be here

  // Additional Web API settings
  let phoneIndexes = GitHubRepository.getPhoneIndexes()
  config.Services.Replace(typeof<IHttpControllerActivator>, CompositionRoot(phoneIndexes))
```
We have done an optimization by getting the phone indexes from the github when the application is starting, so that any requests to the application doesn't get the data from github (typical enterprise performance improvement!).

### Manufacturers Api

We are going to create the Manufacturers Api in the same way as Promotions Api. Let's start from ```ManufacturersController```

```fsharp
namespace Web.Controllers

open System.Web.Http
open PhoneCat.Domain

type ManufacturerViewModel = { Name : string }

[<RoutePrefix("api/manufactures")>]
type ManufacturersController
  (
    getManufacturerNames : seq<Phone> -> seq<ManufacturerName>,
    phones : seq<Phone>              
  ) = 
  inherit ApiController()

  [<Route("")>]
  member this.Get () =
    getManufacturerNames phones
    |> Seq.distinct
    |> Seq.map (fun name -> {Name = ManufacturerName.ToString name})
```

As ```PromotionsController```, ```ManufacturersController``` has also two dependencies.

* ```getManufacturerNames``` is a function takes a sequence of domain model ```Phone``` and returns the sequence of domain model ```ManufacturerName``` which represents the manufacturer names of the given phones.

* ```phones``` is a sequence of domain model ```Phone`` that represents the phones available in the backend data store

The ```Get``` method in the ```ManufacturersController``` returns the manufacturer names for the given phones

The ```ManufacturerViewModel``` is just a DTO that is used to share the data across the wire.

The next step is to add the domain models. In the ```Production``` file of *Domain* project add the following

```fsharp
type ManufacturerName = 

  Samsung | Motorola | Dell | LG | TMobile | Sanyo | Unknown

  static member ToString = function           
      | Samsung -> "Samsung"
      | Motorola -> "Motorola"
      | Dell -> "Dell"
      | LG -> "LG"
      | TMobile -> "T-Mobile"
      | Sanyo -> "Sanyo"
      | Unknown -> "Unknown"

  static member ToManufacturerName (name : string) =
      match name.ToLower() with
      | n when n.Contains("samsung") -> Samsung
      | n when n.Contains("motorola") -> Motorola
      | n when n.Contains("dell") -> Dell
      | n when n.Contains("lg") -> LG
      | n when n.Contains("t-mobile") -> TMobile
      | n when n.Contains("sanyo") -> Sanyo
      | n when n.Contains("nexus") -> Samsung
      | _ -> Unknown

type Phone = 
  { Id : string
    Name : string
    Description : string
    ImageUrl : string }
```
As the backend data-store doesn't directly gives the manufacturer name, we will be adding two **static** members that derives the manufacturer name from the phone name. The ```ToString``` function translates to the domain model to its equivalent string version

After defining the domain models, add the business logic for the getting the manufacturer names from the phone in the new source file ```Phones``` of the *Domain* project.

```fsharp
namespace PhoneCat.Domain

[<AutoOpen>]
module Phones =    
    
  let getManufacturerNames (phones : seq<Phone>) = 
    phones
    |> Seq.map (fun p -> ManufacturerName.ToManufacturerName p.Name)

```
The ```getManufacturerNames``` is a straight forward function that map given the phones to its equivalent manufacture names.

The next step would be fetching the phones from the data-store. 

Let's start by defining a type provider for the phone. Update the ```TypeProviders``` module in the *DataAccess* project as below

```fsharp
[<Literal>]
let private samplePhoneJson = """
{
  "additionalFeatures": "Trackball", 
  "android": {
      "os": "Android 2.2", 
      "ui": ""
  }, 
  "availability": [
      "Sprint"
  ], 
  "battery": {
      "standbyTime": "", 
      "talkTime": "4 hours", 
      "type": "Lithium Ion (Li-Ion) (1130 mAH)"
  }, 
  "camera": {
      "features": [
          "Video"
      ], 
      "primary": "3.2 megapixels"
  }, 
  "connectivity": {
      "bluetooth": "Bluetooth 2.1", 
      "cell": "CDMA2000 1xEV-DO Rev.A", 
      "gps": true, 
      "infrared": false, 
      "wifi": "802.11 b/g"
  }, 
  "description": "Zio uses CDMA2000 1xEV-DO rev", 
  "display": {
      "screenResolution": "WVGA (800 x 480)", 
      "screenSize": "3.5 inches", 
      "touchScreen": true
  }, 
  "hardware": {
      "accelerometer": true, 
      "audioJack": "3.5mm", 
      "cpu": "600MHz Qualcomm MSM7627", 
      "fmRadio": false, 
      "physicalKeyboard": false, 
      "usb": "USB 2.0"
  }, 
  "id": "sanyo-zio", 
  "images": [
      "img/phones/sanyo-zio.0.jpg", 
      "img/phones/sanyo-zio.1.jpg", 
      "img/phones/sanyo-zio.2.jpg"
  ], 
  "name": "SANYO ZIO", 
  "sizeAndWeight": {
      "dimensions": [
          "58.6 mm (w)", 
          "116.0 mm (h)", 
          "12.2 mm (d)"
      ], 
      "weight": "105.0 grams"
  }, 
  "storage": {
      "flash": "130MB", 
      "ram": "256MB"
  }
}

"""

type PhoneTypeProvider = JsonProvider<samplePhoneJson>

let ToPhone (phone : PhoneTypeProvider.Root) =
    { Id = phone.Id; Name = phone.Name; Description = phone.Description; ImageUrl = phone.Images.[0] }
``` 
Like ```ToPhoneIndex``` utility function,  ```ToPhone``` converts the data store model ```PhoneTypeProvider.Root``` to the domain model ```Phone```

Unlike the phone index, [angular-phonecat](https://github.com/angular/angular-phonecat/tree/master/app/phones) repository stores the details of each phones in a seperate json file with the naming convention of **{phoneId}.json**. So, in order to get the details of all the phones, we need to fire multiple requests to get the details. Thanks to [fsharp async workflow](http://blogs.msdn.com/b/dsyme/archive/2010/01/09/async-and-parallel-design-patterns-in-f-parallelizing-cpu-and-i-o-computations.aspx) we can easily pull all the data parallelly and populate the strongly typed domain models from them.

Update the ```GitHubRepository``` module with the code to populate the data from the data store.

```fsharp
namespace PhoneCat.DataAccess
open PhoneCat.Domain

module GitHubRepository = 

  [<Literal>]
  let private gitHubRepoUrl = "https://raw.githubusercontent.com/angular/angular-phonecat/master/app/phones/"
  
  let private phoneIndexes = PhoneIndexTypeProvider.Load(gitHubRepoUrl + "phones.json")

  let private phoneIds = phoneIndexes |> Seq.map (fun p -> p.Id)

  let private pMap ids =
      seq {for id in ids -> async { return id, PhoneTypeProvider.Load(gitHubRepoUrl + id + ".json") }}
      |> Async.Parallel
      |> Async.RunSynchronously

  let private phones = pMap phoneIds |> Map.ofSeq

  let getPhoneIndexes () = phoneIndexes

  let getPhones () = phones |> Seq.map (fun p -> p.Value)
```
Expressiveness is one of the reason behind my fsharp addiction. Just see the above code we have done some much thing with just few lines of code. We have fetched the details of all the phones using their ids from phone index and created an in-memory map.

The ```getPhones``` function returns the strongly typed ```PhoneTypeProvider``` models from the in-memory map.

The last thing is to wire up all the things and create the ```ManufacturersController```. Update the ```CompositionRoot``` type in the ```Infrastructure``` module with the below code

```fsharp
type CompositionRoot 
  (
    phones : seq<PhoneTypeProvider.Root>,
    phoneIndexes : seq<PhoneIndexTypeProvider.Root>  
  ) =                
  interface IHttpControllerActivator with
  member this.Create(request, controllerDescriptor, controllerType) =
    
    let phones' = phones |> Seq.map TypeProviders.ToPhone
    let phoneIndexes' = phoneIndexes |> Seq.map TypeProviders.ToPhoneIndex

    if controllerType = typeof<PromotionsController> then
        let promotionsController = 
          new PromotionsController(getPromotions, phoneIndexes')
        promotionsController :> IHttpController

    else if controllerType = typeof<ManufacturersController> then
        let manufacturersController = 
          new ManufacturersController(getManufacturerNames, phones')
        manufacturersController :> IHttpController 
    else
        raise <| ArgumentException((sprintf "Unknown controller type requested: %A" controllerType))
```

And update the *Global.asax.fs* as below

```fsharp
static member RegisterWebApi(config: HttpConfiguration) =
       
  // existing configuration will be here

  // Additional Web API settings
  let phones = GitHubRepository.getPhones()
  let phoneIndexes = GitHubRepository.getPhoneIndexes()
  config.Services.Replace(typeof<IHttpControllerActivator>, CompositionRoot(phones, phoneIndexes))
```

### Top Selling Phones Api

Create ```PhonesController``` in the *Web* project and add the api to return the top selling phones

```fsharp
namespace Web.Controllers

open System.Web.Http
open PhoneCat.Domain

[<RoutePrefix("api/phones")>]
type PhonesController
  (
    getTopSellingPhones : int -> seq<Phone> -> seq<Phone>,
    phones : seq<Phone>              
  ) = 
  inherit ApiController()

  [<Route("topselling")>]
  member this.GetTopSelling () =
    getTopSellingPhones 3 phones
```

The ```getTopSellingPhones``` picks given count of phones from the given phones and return them.

In the ``Phones`` module of *Domain* project add the function ```getTopSellingPhones```

```fsharp
let getTopSellingPhones (phonesSold : seq<PhoneSold>) phoneCount (phones : seq<Phone>) = 
  phonesSold
  |> Seq.map (fun p -> p.Quantity, (phones |> Seq.find (fun p' -> p'.Id = p.Id)))
  |> Seq.sortBy (fun x -> -(fst x))
  |> Seq.take phoneCount
  |> Seq.map snd
```

The ```PhoneSold``` domain model represents how much quantity of each phone has been sold. The ```getTopSellingPhones``` sorts the given phones in the descending order based on the quantity being sold and return the given count of the phones from the sorting result

Right now the ```PhoneSold``` domain model is not added. So add them in the new module ```Purchases``` in the *Domain* project as below

```fsharp
namespace PhoneCat.Domain

[<AutoOpen>]
module Purchases =    
  type PhoneSold = {
    Id : string
    Quantity : int
  } 
```

The angular data-store doesn't provide quantity sold information, so we need to get it from somewhere else. To keep it simple, we are going add an ```InMemoryInventory``` in *DataAccess* project as below

```fsharp
namespace PhoneCat.DataAccess
open PhoneCat.Domain

module InMemoryInventory =
  let getPhonesSold () = [
    { Id = "motorola-xoom-with-wi-fi"; Quantity = 10}
    { Id = "motorola-atrix-4g"; Quantity = 24}
    { Id = "nexus-s"; Quantity = 4}
    { Id = "samsung-galaxy-tab"; Quantity = 41}
    { Id = "sanyo-zio"; Quantity = 31}
    { Id = "motorola-xoom"; Quantity = 6}
  ]
```

As previous api's we need wire things up and create the controller in the ```CompositeRoot```

```fsharp
 else if controllerType = typeof<PhonesController> then                    
  let getTopSellingPhones = Phones.getTopSellingPhones (InMemoryInventory.getPhonesSold())                    
  let phonesController = new PhonesController(getTopSellingPhones, phones') 
  phonesController :> IHttpController
```
We have used [partial application](http://fsharpforfunandprofit.com/posts/partial-application/) of the ```Phones.getTopSellingPhones``` function here. Its signature is ```seq<PhoneSold> -> int -> seq<Phone> -> seq<Phone>```
but the controller expects the function with the signature ```int -> seq<Phone> -> seq<Phone>```.

The expression

```fsharp
let getTopSellingPhones = Phones.getTopSellingPhones (InMemoryInventory.getPhonesSold())      
```
actually does this.

### Front-End

As front-end is out of the scope of this blog post I am not covering it here. I've actually developed it using knockout.js.

## Summary

We have covered a quite a large ground here by creating three web-api endpoints in fsharp using Web Api 2. In the later posts we will be adding error handling and making it robust. The source code for this step can be found in the [github repository](https://github.com/tamizhvendan/fsharp-phonecat/tree/1)