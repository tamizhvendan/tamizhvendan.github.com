---
layout: post
title: "Step-8 Adding Shopping Cart"
date: 2015-03-20 11:49:35 +0530
comments: true
categories: 
  - fsharp
  - web-api
---

> This is step 8 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In the [last blog post]({% post_url 2015-03-02-step-7-validation-and-error-handling-using-rop %}) we have seen how to do validation and error handling in fsharp and in this blog post we are going to add shopping cart to the phone-cat application that we are developing in this blog series. To keep this blog post simple we are going to see only adding an item to shopping cart. Remvoing an item from shopping cart is a straight forward implementation and I leave it to you as an exercise

{% img /images/fsharp_phonecat/step_8/cart_intro.png %}

### The Shopping Cart Domain

Let's start with defining the domain model for handling shopping cart. Create a source file ```ShoppingCart``` in the **Domain** project and add the following code

```fsharp
module ShoppingCart =    

  type ProductId = string
    
  type Cart =
    | Empty
    | Active of ProductId List

  let addItem cart productId =
    match cart with
    | Empty -> Active [productId]
    | Active items -> Active (productId :: items)
```

The ```ProductId``` is an alias of ```string``` type represents the id of the phone.

The ```Cart``` is a ```Sum``` type represents the two possible state of Shopping Cart. The ```Cart``` would be either empty or contain product ids that has been added to the cart.

We have added a function which adds a product id to the cart. It just checks the state of the cart and based on the state it either creates the cart with ```Active``` state or appends the product id to the existing list.

Thanks to the strong fsharp type system, we have modeled the domain with less lines of code.

### Persisting Shopping Cart

Now we have the domain logic to represent the shopping cart and adding item to it. The next step is persisting the cart. We can persist it any where as the domain model is persistant ignorant. To keep things simple we will be persiting it in-memory. 

As we did during the [recommendation step]({% post_url 2015-01-02-step-3-phonecat-recommendation-system-using-f-number-agents %}) we will be using a dictionary to store the cart with Asp.Net's [anonymousId](http://msdn.microsoft.com/en-us/library/system.web.httprequest.anonymousid%28v=vs.110%29.aspx) as the key. 

Create a source file ```CartStorage``` in the **DataAccess** project and update it as below

```fsharp
module CartStorage =  
  
  let private inMemoryStorage = 
    new Dictionary<string, Cart>()

  let create anonymousId cart = 
    inMemoryStorage.Add(anonymousId, cart)
    cart

  let update anonymousId cart =
    inMemoryStorage.Remove(anonymousId) |> ignore
    create anonymousId cart

  let get anonymousId =
    match inMemoryStorage.ContainsKey(anonymousId) with
    | true -> Some inMemoryStorage.[anonymousId] 
    | _ -> None
```

The code is very straight forward to understand. It contains typcial CRUD operations of shopping cart using an in-memory dictionary object.