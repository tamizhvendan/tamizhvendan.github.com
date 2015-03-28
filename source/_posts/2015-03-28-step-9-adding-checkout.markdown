---
layout: post
title: "Step-9 Adding Checkout"
date: 2015-03-28 13:07:20 +0530
comments: true
categories:
  - fsharp
  - ASP.NET MVC 
---

> This is step 9 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In the [last blog post]({% post_url 2015-03-20-step-8-adding-shopping-cart%}) we have added the shopping cart feature to our phonecat application and in this blog post we are going to implement the checkout feature.

Implementing the checkout feature involves two steps.
  1. Adding a View Cart Page
  2. Processing the Checkout and creating an order

### Adding a View Cart Page

{% img border /images/fsharp_phonecat/step_9/view-cart.png %}

As we have done in the previous posts, let's begin by extending our shopping cart domain. The first step is to add a function to retrieve the items in the shopping cart. 

Open ```ShoppingCart``` module in the **Domain** project and update it as below

```fsharp
let getItems cart =
  match cart with
  | Empty -> Seq.empty
  | Active items -> items |> List.toSeq
``` 

The above function retrieves the phone ids present in the cart. In order to show a detailed checkout page, we need to get the associated phone details of these phone ids. Right now we don't have this capability in our app so let's add them.

Open ```Phones``` module in the **Domain** project and add the below functions

```fsharp
let contains x = Seq.exists ((=) x)

let getPhonesByIds (phones : seq<Phone>) phoneIds =  
  phones |> Seq.filter (fun p -> contains p.Id phoneIds)
``` 

The ```contains``` function is a utility function which checks whether the given element is present in a ```seq``` or not and the ```getPhonesByIds``` function filters phones in the system by the incoming phone ids and returns the requested ones.


Now we have all the bare bones and the next step is defining a controller to serve the View Cart Page.

Add a new Source file ```CheckoutController``` in the **Web** project under *Controllers* directory and update it as below

```fsharp
type CheckoutController
  (
      getPhones : seq<string> -> seq<Phone>,
      cart : Cart
  ) = 
  inherit Controller ()

  member this.ViewCart () =
    cart
    |> getItems 
    |> getPhones
    |> this.View
```

The ```ViewCart``` action method uses the two functions that we have defined before and renders the ```ViewCart``` view. Add a new razor view with the name ```ViewCart``` in the *Views\Checkout* Directory and update it [as described here](https://github.com/tamizhvendan/fsharp-phonecat/blob/9/Web/Views/Checkout/ViewCart.cshtml#L1-L30). 

The final step in rendering this view is gluing the components together in the ```CheckoutController``` controller instance creation. 

To get the cart associated with the given session id we have added [a piece of logic](https://github.com/tamizhvendan/fsharp-phonecat/blob/8/Web/Infrastructure.fs#L41-L45) in the last step which checks the presence of ```Cart``` for the given anonymous id in the ```CartStorage``` and creates the cart if it doesn't contain. Since we need the same logic here, let's move this logic to a function ```getOrCreate``` in the ```CartStorage``` module and reuse it in both the places

```fsharp
let getOrCreate anonymousId =
  match get anonymousId with
  | Some cart -> cart
  | None -> create anonymousId ShoppingCart.Empty
```
Then open ```MvcInfrastructure``` module in the **Web** project and update it as below

```fsharp
type CompositionRoot(phones : seq<PhoneTypeProvider.Root>) =          
  inherit DefaultControllerFactory() with
    override this.GetControllerInstance(requestContext, controllerType) = 
    // ... existing code ...
    else if controllerType = typeof<CheckoutController> then
      let anonymousID = requestContext.HttpContext.Request.AnonymousID
      let shoppingCart = CartStorage.getOrCreate anonymousID
      let getPhonesByIds = phones |> Seq.map TypeProviders.ToPhone |> Phones.getPhonesByIds
      let checkoutController = new CheckoutController(getPhonesByIds, shoppingCart)
      checkoutController :> IController
    // ... existing code ...
```

The composition of ```CheckoutController``` is similar to that of other controllers in the application. We get the cart associated with the given anonymous id and then create a partial applied function of ```Phones.getPhonesByIds``` by passing the first argument (phones in the app) alone.

That's it. The Cart View is up and running!

### Processing the Checkout

As mentioned in the beginning, the next step after displaying the shopping cart page is processing the checkout and creating an order. 

Let's begin by defining the domain for Orders.

Create a source file with the name ```Orders``` in the **Domain** project and add the following types

```fsharp
module Orders =
  type OrderRequest = {
    UserId : string
    ProductIds : ProductId seq
  }
  type OrderConfirmed = {
    OrderId : string
    UserId : string
    ProductIds : ProductId seq
  }
```

The ```OrderRequest``` type represents the order being placed by the user and the ```OrderConfirmed``` type represents the successful order creation.

To Keep things simple, we are just going to see the order confirmation and we are intentionally leaving out error handling and validation. We can easily [add them as we did]({% post_url 2015-03-02-step-7-validation-and-error-handling-using-rop %})  it in the user registration step and I leave it you as an exercise.

The next step is storing the incoming order. 

Create a source file with the name ```OrderStorage``` in the **DataAccess** project and add the following function

```fsharp
module OrderStorage =  
  let placeOrder (orderRequest : OrderRequest) =
    let orderId = Guid.NewGuid().ToString()
    {OrderId = orderId; UserId = orderRequest.UserId; ProductIds = orderRequest.ProductIds}
```

Here we are just faked the implementation of order storage by generating a new ```Guid``` and returning it as Order Id

Now it's time for adding a ```OrderController``` in the **Web** project to handle the checkout and creating the order using the components defined above.

```fsharp
type OrderController
  (
    cart : Cart,
    placeOrder : OrderRequest -> OrderConfirmed,
    updateCart : Cart -> Cart
  ) =
  inherit Controller ()

  [<Authorize>]
  member this.Checkout () =
    let identity = base.User.Identity :?> ClaimsIdentity
    let userId = identity.FindFirst(ClaimTypes.Name).Value
    let orderConfirmed = placeOrder { UserId = userId; ProductIds = getItems cart}
    updateCart Cart.Empty |> ignore
    this.View(orderConfirmed);
```

The checkout method is straightforward. We get the ```UserId``` from the ```User.Identity``` property and place the order using it. Upon successful order confirmation, we are clearing the shopping cart and rendering the ```Checkout``` view.

{% img border /images/fsharp_phonecat/step_9/order-confirmation.png %}

The important thing to notice here the ```Authorize``` attribute. Just like any other e-commerce portal, we are not allowing anonymous check out here. The presence of [this authorize attribute](https://msdn.microsoft.com/en-us/library/system.web.mvc.authorizeattribute%28v=vs.118%29.aspx) ensures that only logged in users are checking out and in case if they are not logged in it would redirect the user to the login page.

The last step is updating the composition root to create the ```OrderController``` instance.

Open ```MvcInfrasturcture``` and update it as below

```fsharp
type CompositionRoot(phones : seq<PhoneTypeProvider.Root>) =          
  inherit DefaultControllerFactory() with
    override this.GetControllerInstance(requestContext, controllerType) = 
    // ... existing code ...
    else if controllerType = typeof<OrderController> then
      let anonymousID = requestContext.HttpContext.Request.AnonymousID
      let shoppingCart = CartStorage.getOrCreate anonymousID
      let updateCart = CartStorage.update anonymousID
      let orderController = new OrderController(shoppingCart, OrderStorage.placeOrder, updateCart)
      orderController :> IController
    // ... existing code ...
```

We are done!

### Summary

We have come a long way from just setting up the visual studio project to a complete prototype of an e-commerce site and I hope you would have enjoyed this series of blog post. In the next blog post we are going to refactor the ```CompositionRoot```which is getting clumsy with a series of ```if..else if..``` expressions using Active Patterns and I will be wrapping this series by an another post which will be reflecting back on what I have learned by writing this blog series.

You can find the source code of this step in the [fsharp-phonecat github repository](https://github.com/tamizhvendan/fsharp-phonecat/tree/9)

Thanks for reading!