---
layout: post
title: "Scale Up Azure Functions in F# using Suave"
date: 2016-09-19 15:55:45 +0530
comments: true
categories:
  - fsharp
  - suave
  - Azure-Functions
---

Recently Microsoft Azure has made [F# as a first-class citizen](https://blogs.msdn.microsoft.com/appserviceteam/2016/09/01/azure-functions-0-5-release-august-portal-update/) to write [Azure Functions](https://azure.microsoft.com/en-in/services/functions/). As F# is a functional-first programming language, I feel Azure Functions and F# would be a match made in heaven. In this blog post you are going to experience a scaled up version of Azure Functions in F# using [Suave](https://suave.io)

## What's in the Function Signatures?

In a functional programming language, we define small functions that do one thing well and then we compose them together to define the solution. To [compose functions](https://fsharpforfunandprofit.com/posts/function-composition/), we need to be thoughtful while defining a function.

Let's see the function signature of an [Azure Function in F#](https://azure.microsoft.com/en-us/documentation/articles/functions-reference-fsharp/)

```fsharp
// HttpRequestMessage -> HttpResponseMessage
let Run(req: HttpRequestMessage) =
  new HttpResponseMessage(HttpStatusCode.OK)
```

The `Run` function takes a `HttpRequestMessage` and returns the `HttpResponseMessage`. This signature is simple but it has a limitation. The limitation has been showcased in the [templates](https://github.com/Azure/azure-webjobs-sdk-templates/tree/dev/Templates) directory of Azure Webjobs SDK

{% img center border /images/AzureFunctionsSuave/Azure_Functions_CRUD.png 250 250 %}

My each `C`, `R`, `U`, `D` are in different functions. Well, there is nothing wrong here. These templates are meant for getting started with these things in Azure Functions. But what you will do if you have a requirement to expose `CRUD` of a resource as a Azure Functions?

One option is to define each part of the `CRUD` as separate Azure Functions (as defined by the templates). If you choose to go by this, you will be having four different endpoints and I am sure your client code will have hard time to consume these endpoints. In addition to this, you will also need to manage four things to satisfy your one requirement, exposing a `CRUD` operation as Azure Function.




