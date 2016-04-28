---
layout: post
title: "Expressing Business Logic with F#'s Partial Active Patterns"
date: 2016-04-28 20:16:14 +0530
comments: true
categories:
  - fsharp
  - CleanCode
  - Craftsmanship
---

Checking the state of the system and performing a requested action only if certain conditions met, is an indispensable requirement in most of the software we write. Though it is a straightforward thing to do, things will get complex/messier when we need to check multiple conditions.

Recently I encountered such a situation while developing a sample application for [my fsharp book](https://twitter.com/tamizhvendan/status/713365205871235072). Initially, I solved it with a typical nested `if else` and then I improved it by applying fsharp's [Partial Active Patterns](http://www.developerfusion.com/article/133772/pattern-matching-in-f-part-2-active-patterns/).

In this blog post , I will be sharing what exactly I did and how it can help you to write an elegant code in fsharp.

## Business Domain

Let's start with a description of the problem domain that we are going to solve.

When an individual visits a restaurant, he can place an order, specifying the list of foods he wants. The foods he ordered will be prepared by the chef and then it will be served.

Preparing a food should satisfy the following conditions

* The Food should be a part of the order
* It should not be prepared already
* It should not be served already

Serving a food should satisfy the following conditions

* The Food should be a part of the order
* It should be prepared already
* It should not be served already


If serving a food completes the order, we should tell that the order is served otherwise we should tell that there are some other foods to be served.

## Starting with the types

Let's start with the types that are needed to solve our problem. If you would like to know why we need to start with the types do check out [this wonderful blog post](http://tomasp.net/blog/type-first-development.aspx/) by [Tomas Petricek](https://twitter.com/tomaspetricek).

```fsharp
type Food = {
  MenuNumber : int
  Name : string
}

type Order = {
  Foods : Food list
}

type InProgressOrder = {
  PlacedOrder : Order
  PreparedFoods : Food list
  ServedFoods : Food list
}
```

The names of the types and its properties clearly describe what they represent, so let's get to the crux of the problem straight

## Prepare Food

```fsharp
let prepareFood food ipo =
  if List.contains food ipo.PlacedOrder.Foods  then
    if not (List.contains food ipo.PreparedFoods) then
      if not (List.contains food ipo.ServedFoods) then
          printfn "Prepare Food"
      else
        printfn "Can not prepare already served food"
    else
      printfn "Can not prepare already prepared food"
  else
    printfn "Can not prepare non ordered food"
```

The `if else` conditions represent the criteria mentioned in the business domain and for the sake of simplicity we are just writing the action in the console.

## Serve Food

Let's move on to serving food

```fsharp
let isServingFoodCompletesOrder food ipo =
  let nonServedFoods =
    List.except ipo.ServedFoods ipo.PlacedOrder.Foods
  nonServedFoods = [food]

let serveFood food ipo =
  if List.contains food ipo.PlacedOrder.Foods  then
    if List.contains food ipo.PreparedFoods then
      if not (List.contains food ipo.ServedFoods) then
        if isServingFoodCompletesOrder food ipo then
          printfn "Order Served"
        else
          printfn "Still some food(s) to serve"
      else
        printfn "Can not serve already served food"
    else
      printfn "Can not serve unprepared food"
  else
    printfn "Can not serve non ordered food"
```

This is called [Arrow Anti Pattern](http://c2.com/cgi/wiki?ArrowAntiPattern) and it is obvious that this code is hard to understand and maintain.

It can be improved by using the techniques mentioned [in this StackExchange's answers](http://programmers.stackexchange.com/questions/47789/how-would-you-refactor-nested-if-statements) and also by using the [specification pattern](https://en.wikipedia.org/wiki/Specification_pattern) from the OO World.

Though the specification pattern solves the problem, it has a lot of code! No offense here but it can be done in a better way.

## F# Active Patterns

> Active Patterns allow programmers to wrap arbitrary values in a union-like data structure for easy pattern matching. For example, its possible wrap objects with an active pattern, so that you can use objects in pattern matching as easily as any other union type. - [F# Programming Wiki](https://en.wikibooks.org/wiki/F_Sharp_Programming/Active_Patterns)

Let's begin by defining a partial active pattern for `NonOrderedFood` and `UnPreparedFood`

```fsharp
let (|NonOrderedFood|_|) order food =
  match List.contains food order.Foods with
  | false -> Some food
  | true -> None

let (|UnPreparedFood|_|) ipo food =
  match List.contains food ipo.PreparedFoods with
  | false -> Some food
  | true -> None
```

Then for `AlreadyPreparedFood` and `AlreadyServedFood`

```fsharp
let (|AlreadyPreparedFood|_|) ipo food =
  match List.contains food ipo.PreparedFoods with
  | true -> Some food
  | false -> None

let (|AlreadyServedFood|_|) ipo food =
  match List.contains food ipo.ServedFoods with
  | true -> Some food
  | false -> None
```

Finally,

```fsharp
let (|CompletesOrder|_|) ipo food =
  let nonServedFoods =
    List.except ipo.ServedFoods ipo.PlacedOrder.Foods
  match nonServedFoods = [food] with
  | true -> Some food
  | false -> None
```

With this in place we can rewrite `serveFood` function as

```fsharp
let serveFood food ipo =
  match food with
  | NonOrderedFood ipo.PlacedOrder _ ->
    printfn "Can not serve non ordered food"
  | UnPreparedFood ipo _ ->
    printfn "Can not serve unprepared food"
  | AlreadyServedFood ipo _ ->
    printfn "Can not serve already served food"
  | CompletesOrder ipo _ ->
    printfn "Order Served"
  | _ -> printfn "Still some food(s) to serve"
```

> The goal is to have code that scrolls vertically a lot… but not so much horizontally. - [Jeff Atwood](http://blog.codinghorror.com/flattening-arrow-code/)

Now the code expressing the business logic more clearly

To refactor `prepareFood` to use the parial active patterns we need one more. So, let's define it

```fsharp
let (|PreparedFood|_|) ipo food =
  match List.contains food ipo.PreparedFoods with
  | true -> Some food
  | false -> None
```

Now we all set to refactor `prepareFood` and here we go

```fsharp
let prepareFood food ipo =
  match food with
  | NonOrderedFood ipo.PlacedOrder _ ->
    printfn "Can not prepare non ordered food"
  | PreparedFood ipo _ ->
    printfn "Can not prepare already prepared food"
  | AlreadyServedFood ipo _ ->
    printfn "Can not prepare already served food"
  | _ -> printfn "Prepare Food"
```

Awesome!

You can get the complete code in [my github repository](https://github.com/tamizhvendan/blog-samples/tree/master/RefactoringIfElse/src)

## Summary

>  F# is excellent at concisely expressing business and domain logic - [ThoughtWorks Tech Radar 2012](https://www.thoughtworks.com/radar/languages-and-frameworks/f) 