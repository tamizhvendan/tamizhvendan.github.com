---
layout: post
title: "Step 1 - Phonecat backend using Web Api and TypeProviders"
date: 2014-12-17 17:31:44 +0530
comments: true
categories: 
  - fsharp
  - functional-programming
  - ASP.NET MVC
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

The scaffolding is ready then the next step is to add the actual domain method ```getPromotions``` which does the actual business logic of giving recently launched phones.

Add the ```getPromotions``` function in the ```Promotions``` file as below

```fsharp

let getPromotions (phoneIndexes : seq<PhoneIndex>) = 
  phoneIndexes 
  |> Seq.filter (fun phone -> phone.Age < 3) 
  |> Seq.map (fun phone -> {Id = phone.Id; Name = phone.Name; ImageUrl = phone.ImageUrl})

```

The ```getPromotions``` just filters the phones with the age less than 3 and map them to the corresponding ```PromotionPhone``` types.

