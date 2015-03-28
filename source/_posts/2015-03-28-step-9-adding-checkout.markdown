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

As we have did in the previous posts let's begin with extending our shopping cart domain. The first step is to add a function to retrieve the items in the shopping cart. 

Open ```ShoppingCart``` module in the **Domain** project and update it as below

```fsharp
let getItems cart =
  match cart with
  | Empty -> Seq.empty
  | Active items -> items |> List.toSeq
``` 

The above function retrieves the phone ids present in the cart. In order to show a detailed checkout page we need to get the associated phone details of these phone ids. Right now we don't have this capability in our app so let's add them.

Open ```Phones``` module in the **Domain** project and add the below functions

```fsharp
let contains x = Seq.exists ((=) x)

let getPhonesByIds (phones : seq<Phone>) phoneIds =  
  phones |> Seq.filter (fun p -> contains p.Id phoneIds)
``` 

The ```contains``` function is an utility function which checks whether the given element is present in a ```seq``` or not and the ```getPhonesByIds``` function filters phones in the system by the incoming phone ids and returns the requested ones.


Now we have all 