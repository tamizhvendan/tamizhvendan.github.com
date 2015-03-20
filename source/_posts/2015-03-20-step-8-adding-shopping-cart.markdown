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

### Shopping Cart Api

It's time to code the api for the shopping cart. Let's add a source file ```ShoppingCartController``` in the **Web** project and update it as below

```fsharp
[<RoutePrefix("api/cart")>]
type ShoppingCartController 
  (
    cart : Cart,
    updateCart : Cart -> Cart
  ) =
  inherit ApiController () 

  [<Route("")>]
  member this.Get() = cart

  [<HttpPost>]
  [<Route("add")>]
  member this.AddItem([<FromBody>]productId : string) = 
    addItem cart productId |> updateCart
```

The ```ShoppingCartController``` has two dependencies.
  1. ```Cart``` - Cart associated with the current session
  2. ```updateCart``` - a function to update the cart

To keep it simple, I haven't added error handling or validation here. The action method ```Get``` returns the cart and the action method ```AddItem``` adds the item to the cart and then update the cart in the storage.


### Wiring things up

Now we have all the pieces to add shopping cart to our application and the final step is tying them together. As we did it in other steps we need to do it in the composition root. Open ```Infrastructure``` in the **Web** project and update it as below

```fsharp
type CompositionRoot 
  (
    // ... Exisitng Code ...
  ) = 
  interface IHttpControllerActivator with           
    member this.Create(request, controllerDescriptor, controllerType) =
      // ... Exisiting Code ...
      else if controllerType = typeof<ShoppingCartController> then                  
        let anonymousID = HttpContext.Current.Request.AnonymousID
        let shoppingCart = 
          match CartStorage.get anonymousID with
          | Some cart -> cart
          | None -> CartStorage.create anonymousID ShoppingCart.Empty
                                                                                  
        let shoppingCartController = new ShoppingCartController(shoppingCart, CartStorage.update anonymousID)
        shoppingCartController :> IHttpController
      // ... Existing Code ...
```

Here before the creating an instance of ```ShoppingCartController``` we retreive the anonymous id from the incoming request and get the cart associated with the anonymous id from the storage. In case if the cart is not available we are creating one. For the ```update``` function we pass the partially applied ```update``` function of ```CartStorage```.

That's it !!

### The front-end code

In the front end we just call the ```ShoppingCartController``` action methods using plain jQuery ajax methods and update the UI. Since it is out of the scope of this blog post I haven't covered it here and you can find [the code here](https://github.com/tamizhvendan/fsharp-phonecat/blob/8/Web/Scripts/site.js).

### Summary

In this blog post we have seen how to implement shopping cart in a web application written in fsharp. I leave some exercises to you to extend it to use some other storage and also to add validation and error handling. You can find the source code in the [github phonecat repository](https://github.com/tamizhvendan/fsharp-phonecat/tree/8).