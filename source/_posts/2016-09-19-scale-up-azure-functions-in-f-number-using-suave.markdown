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

Recently Microsoft Azure has made [F# as a first-class citizen](https://blogs.msdn.microsoft.com/appserviceteam/2016/09/01/azure-functions-0-5-release-august-portal-update/) to write [Azure Functions](https://azure.microsoft.com/en-in/services/functions/). As F# is a functional-first programming language, I feel Azure Functions and F# would be a match made in heaven.

In this blog post, you are going to experience a scaled up version of Azure Functions in F# using [Suave](https://suave.io)

## What's in the Function Signatures?

In a functional programming language, we define small functions that do one thing well and then we compose them together to represent the solution. To [compose functions](https://fsharpforfunandprofit.com/posts/function-composition/), we need to be thoughtful while designing the signature of a function.

Let's see the signature of an [Azure Function in F#](https://azure.microsoft.com/en-us/documentation/articles/functions-reference-fsharp/)

```fsharp
// HttpRequestMessage -> HttpResponseMessage
let Run(req: HttpRequestMessage) =
  new HttpResponseMessage(HttpStatusCode.OK)
```

The `Run` function takes a `HttpRequestMessage` and returns the `HttpResponseMessage`. This signature is simple, but it has a limitation. The limitation has been showcased in the [templates](https://github.com/Azure/azure-webjobs-sdk-templates/tree/dev/Templates) directory of Azure Webjobs SDK

{% img center border /images/AzureFunctionsSuave/Azure_Functions_CRUD.png 250 250 %}

My each `C`, `R`, `U`, `D` are in different functions. Well, there is nothing wrong here. These templates are suitable for getting started in Azure Functions. But what will you do if you have a requirement to expose `CRUD` of a resource as an Azure Functions?

One option is to define each part of the `CRUD` as separate Azure Functions (as defined by the templates). If you choose to go by this, you will have four different endpoints and I am sure your client code will have a hard time to consume these endpoints. In addition to this, you will also need to manage four things to satisfy your one requirement.

The other option is putting the `CRUD` inside a single function.

```fsharp
let Run (req:HttpRequestMessage) =
    if req.Method = HttpMethod.Get then
        // ...
    else if req.Method = HttpMethod.Post then
        // ...
    else if req.Method = HttpMethod.Put then
        // ...
    else if req.Method = HttpMethod.Delete then
        // ...
    else
        // ...
```

Though this approach solves the problem, it comes with another set of [challenges](http://stackoverflow.com/questions/126409/ways-to-eliminate-switch-in-code). In Object Oriented  Programming, we typically use [Polymorphism](http://www.refactoring.com/catalog/replaceConditionalWithPolymorphism.html) to replace the conditional logic.

{% img center http://scontent.cdninstagram.com/t51.2885-15/s480x480/e35/13391359_1083221395099569_1878773023_n.jpg?ig_cache_key=MTI3NTQ5MjQ4NzE4Mjk3MTIxOQ%3D%3D.2 %}

## Revisiting Function Signature

A request `handler` looks for some condition to be meet in the incoming HTTP request, and if the predicate succeeds, it modifies the HTTP response.

The signature of the `Run` function, `HttpRequestMessage -> HttpResponseMessage` is not completely reflecting the above specification.

Let's have a look at the limitations of this signature

* The `Run` function doesn't return the `HttpRequestMessage`. So if we have multiple `handler`s we are constrained to use either `if else if` or Polymorphism.

* It doesn't represent a handler that doesn't handle the HTTP request. If the HTTP request is `GET`, the `handler` for HTTP `POST` will not modify the `HttpResponseMessage`

The better signature would have the following to describe a handler in a better way

* The handler has to be pure function that takes both `Request` and `Response` as it's parameters

* If the handler is not handling the HTTP request, it has to return the unmodified `Request` and `Response` along with an indicator saying that it didn't handle the request.



It's where the [Suave](https://suave.io) library shines. Suave defines a type called `WebPart` with the signature to model the `handler` with the above-said expectations.

```fsharp
type HttpContext = {
  request    : HttpRequest
  response   : HttpResult
  // ...
}

type WebPart = HttpContext -> Async<HttpContext option>
```

> The `Async` represents that the `WebPart` function is a non-blocking asynchronous function and `option` type models the `WebPart` which doesn't handle the HTTP request

The real power of Suave is its set of combinators to manipulate route flow and task composition. You can define an API in Suave that only handles HTTP `POST` requests and returns `Hello` as text without typing too much.

```fsharp
// HttpContext -> Async<HttpContext option>
let app = POST >=> OK "HelloSystem.Net.Http
```

> To learn more about the Suave combinators refer my blog post on [Building REST API in suave]({% post_url 2015-06-11-building-rest-api-in-fsharp-using-suave  %})

If you notice the binding `app` itself is a `WebPart` (which in turn a function) with the signature `HttpContext -> Async<HttpContext option>`. So, you can call this function in your application code and project the output of the function to any output medium that you wish.

## The Difference

The Azure Functions do an incredible job in helping you to define *a part of your system* as *a function*. Suave takes it to the next level by helping you to define *your system* as *function*.

In nutshell, Suave complements Azure Functions and helps you to define your system as a *Serverless Function*

## Creating a Suave Adapter

So, to scale up Azure Functions using Suave, all we need is an adapter.

{% img center http://images.freeimages.com/images/previews/c0d/adapter-1420487.jpg %}

The adapter does the following

* Transforms `HttpRequestMessage` from `System.Net.Http` to `HttpRequest` of `Suave.Http`

* Then create an empty Suave's `HttpContext` with the above `HttpRequest` and call the `WebPart` (that represents your system).

* The final step is converting the `HttpResult` of `Suave.Http` to `HttpResponseMessage` of `System.Net.Http`.


Let's start from `HttpRequestMessage`

```fsharp
// SuaveAdapter.fsx
let SuaveHttpMethod (httpMethod : System.Net.Http.HttpMethod) =
  match httpMethod.Method with
  | "GET" -> HttpMethod.GET
  | "POST" -> HttpMethod.POST
  | "PUT" -> HttpMethod.PUT
  | "DELETE" -> HttpMethod.DELETE
  | x -> HttpMethod.OTHER x

let SuaveHeaders (headers : HttpRequestHeaders) =
  headers
  |> Seq.map (fun h -> (h.Key, h.Value |> Seq.head))
  |> Seq.toList

let SuaveRawForm (content : System.Net.Http.HttpContent) = async {
  let! content = content.ReadAsByteArrayAsync() |> Async.AwaitTask
  return content
}

let SuaveRawQuery (requestUri : System.Uri) =
  if requestUri.Query.Length > 1 then
    requestUri.Query.Substring(1)
  else
      ""

let NetHeaderValue (headers : HttpRequestHeaders) key =
    headers
    |> Seq.tryFind (fun h -> h.Key = key)
    |> Option.map (fun h -> h.Value |> Seq.head)

let SuaveRequest (req : HttpRequestMessage) = async {
  let! content = SuaveRawForm req.Content
  let host = defaultArg (NetHeaderValue req.Headers "Host") ""
  return {HttpRequest.empty with
            url = req.RequestUri            
            ``method`` = SuaveHttpMethod req.Method
            headers = SuaveHeaders req.Headers
            rawForm = content
            rawQuery = SuaveRawQuery req.RequestUri
            host = host}
}
```

> As a convention, I've used `Net` and `Suave` prefixes in the function name to represent the returning type of `System.Net.Http` and `Suave.Http` respectively.  

I hope that these functions are self-explanatory, so let's move on the next step.

> To keep it simple, I've ignored other HTTP Methods like PATCH, HEAD, etc.

The next step is creating Suave `HttpContext`

```fsharp
let SuaveContext httpRequest = async {
  let! suaveReq = SuaveRequest httpRequest
  return { HttpContext.empty with request = suaveReq}
}   
```

Then we need to convert `HttpResult` to `HttpResponseMessage`

```fsharp
let NetStatusCode = function
| HttpCode.HTTP_200 -> HttpStatusCode.OK
| HttpCode.HTTP_201 -> HttpStatusCode.Created
| HttpCode.HTTP_400 -> HttpStatusCode.BadRequest
| HttpCode.HTTP_404 -> HttpStatusCode.NotFound
| HttpCode.HTTP_202 -> HttpStatusCode.Accepted
| _ -> HttpStatusCode.Ambiguous

let NetHttpResponseMessage httpResult =
  let content = function
  | Bytes c -> c
  | _ -> Array.empty
  let res = new HttpResponseMessage()
  let content = new ByteArrayContent(content httpResult.content)
  httpResult.headers |> List.iter content.Headers.Add  
  res.Content <- content
  res.StatusCode <- NetStatusCode httpResult.status
  res
```

> To keep it simple, I've ignored other HTTP StatusCodes

The final step is putting these functions together and run the `WebPart` function with the translated `HttpContext`.

```fsharp
let SuaveRunAsync app suaveContext = async {
  let! res = app suaveContext
  match res with
  | Some ctx ->
    return (NetHttpResponseMessage ctx.response, ctx)
  | _ ->
    let res = new HttpResponseMessage()
    res.Content <- new ByteArrayContent(Array.empty)
    res.StatusCode <- HttpStatusCode.NotFound
    return res,suaveContext
}

let RunWebPartAsync app httpRequest = async {
  let! suaveContext = SuaveContext httpRequest
  return! SuaveRunAsync app suaveContext
}
```

## Suave Adapter In Action

Let's see the Suave Adapter that we created in action.

As already there are two great blog posts by [Greg Shackles](http://gregshackles.com/getting-started-with-azure-functions-and-f/) and [Michał Niegrzybowski](https://mnie.github.io/2016-09-08-AzureFunctions/), I am diving directly into Azure functions in F#.

Let me create a new Azure Function application in Azure with the name "TamAzureFun" and then define the first function `HelloSuave`.

The `function.json` of `HelloSuave` has to be updated with the `methods` property to support different HTTP request methods.

```json
{
    "disabled": false,
    "bindings": [{
      "type": "httpTrigger",
      "name": "req",
      "methods": ["get","put","post","delete"],
      "authLevel": "anonymous",
      "direction": "in"
    },{
      "type": "http",
      "name": "res",
      "direction": "out"
    }]
}
```

Then add the `Suave` dependency in `project.json`

```json
{
  "frameworks": {
    "net46": {
      "dependencies": {
        "Suave": "1.1.3"
      }
    }
  }
}
```

Let's start simply by defining small API (system) that handles different types of HTTP methods.

```fsharp
// app.fsx
open Suave
open Suave.Successful
open Suave.Operators
open Suave.Filters

let app =
  choose [
      GET >=> OK "GET test"
      POST >=> OK "POST test"
      PUT >=> OK "PUT test"
      DELETE >=> OK "DELETE test"]
```

The final step is referring the `SuaveAdapter.fsx` & `app.fsx` files in the `run.fsx` and have fun!

```fsharp
// run.fsx
#load "SuaveAdapter.fsx"
#load "app.fsx"
open SuaveAdapter
open App
open System.Net.Http
open Suave

let Run (req : HttpRequestMessage) =
  let res, _ = RunWebPartAsync app req |> Async.RunSynchronously
  res
```

Let's make some HTTP requests to test our implementation.

{% img center border /images/AzureFunctionsSuave/HelloSuaveRequests.png 700 500 %}

Suave is rocking!


## Creating a REST API in Azure Functions

We can extend the above example to expose a REST end point!

In Suave *a REST API* is a **function**.

Create a new Azure Function `HelloREST` and add `NewtonSoft.Json` & `Suave` dependencies in `project.json`

```json
{
  "frameworks": {
    "net46": {
      "dependencies": {
        "Suave": "1.1.3",
        "NewtonSoft.Json" : "9.0.1"
      }
    }
  }
}
```

To handle *JSON* requests and responses, let's add some combinators

```fsharp
// Suave.Newtonsoft.Json.fsx
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open System.Text
open Suave.Json
open Suave.Http
open Suave
open Suave.Operators

let toJson<'T> (o: 'T) =
  let settings = new JsonSerializerSettings()
  settings.ContractResolver <- new CamelCasePropertyNamesContractResolver()
  JsonConvert.SerializeObject(o, settings)
  |> Encoding.UTF8.GetBytes

let fromJson<'T> (bytes : byte []) =
  let json = Encoding.UTF8.GetString bytes
  JsonConvert.DeserializeObject(json, typeof<'T>) :?> 'T

let mapJsonWith<'TIn, 'TOut>
  (deserializer:byte[] -> 'TIn) (serializer:'TOut->byte[]) webpart f =
  request(fun r ->
    f (deserializer r.rawForm)
    |> serializer
    |> webpart
    >=> Writers.setMimeType "application/json")

let MapJson<'T1,'T2> webpart =
  mapJsonWith<'T1,'T2> fromJson toJson webpart

let ToJson webpart x  =
  toJson x |> webpart >=> Writers.setMimeType "application/json"   
```

Then define the REST api in `app.fsx`

```fsharp
#load "Suave.Newtonsoft.Json.fsx"
open Suave
open Suave.Successful
open Suave.Operators
open Suave.Filters
open System
open Suave.Newtonsoft.Json

type Person = {
  Id : Guid
  Name : string
  Email : string
}

let createPerson person =
  let newPerson = {person with Id = Guid.NewGuid()}
  newPerson

let getPeople () = [
  {Id = Guid.NewGuid(); Name = "john"; Email = "j@g.co"}
  {Id = Guid.NewGuid(); Name = "mark"; Email = "m@g.co"}]

let getPersonById id =
  {Id = Guid.Parse(id); Name = "john"; Email = "j@g.co"}
  |> ToJson ok

let deletePersonById id =
  sprintf "person %s deleted" id |> OK

let app =
  choose [
    path "/people" >=> choose [
      POST >=> MapJson created createPerson
      GET >=> ToJson ok (getPeople ())
      PUT >=> MapJson accepted id
    ]
    GET >=> pathScan "/people/%s" getPersonById
    DELETE >=> pathScan "/people/%s" deletePersonById
  ]
```
> To keep things simple, I am hard coding the values here. It can easily be extended to talk to any data source

Our `SuaveAdapter` has capable of handling different HTTP methods and but it hasn't been programmed to deal with different paths.

Here in this example we need to support two separate paths

```
GET /people
GET /people/feafa5b5-304d-455e-b7e7-13a5b3293f77
```

The HTTP endpoint to call an Azure function has the format

```
https://{azure-function-app-name}.azurewebsites.net/api/{function-name}
```

At this point of writing it doesn't support multiple paths. So, we need to find a workaround to do it.

One way achieving this is to pass the *paths* as a Header. Let's name the Header key as `X-Suave-URL`. Upon receiving the request we can rewrite the URL as

```
https://{azure-function-app-name}.azurewebsites.net/{header-value-of-X-Suave-URL}
```

Let's update `SuaveAdapter.fsx` to do this

```fsharp
let RunWebPartWithPathAsync app httpRequest = async {
  let! suaveContext = SuaveContext httpRequest
  let netHeaderValue = NetHeaderValue httpRequest.Headers
  match netHeaderValue "X-Suave-URL", netHeaderValue "Host"  with
  | Some suaveUrl, Some host ->
    let url = sprintf "https://%s%s" host suaveUrl |> System.Uri
    let ctx = {suaveContext with
                request = {suaveContext.request with
                            url = url
                            rawQuery = SuaveRawQuery url}}
    return! SuaveRunAsync app ctx
  | _ -> return! SuaveRunAsync app suaveContext
}
```

The final step is updating the `run.fsx` file to use this new function

```fsharp
#load "SuaveAdapter.fsx"
#load "app.fsx"
open SuaveAdapter
open App
open System.Net.Http
open Suave

let Run (req : HttpRequestMessage) =
  let res, _ = RunWebPartWithPathAsync app req |> Async.RunSynchronously
  res
```

**Serverless REST API in Action**

{% img center border /images/AzureFunctionsSuave/HelloRestRequests.jpeg %}

> This blog post is a proof of concept to use Suave in Azure Functions. There are a lot of improvements to be made to make it production ready. I am planning to publish this as a NuGet package based on the feedback from the community.

**Update** : [Suave.Azure.Functions](https://www.nuget.org/packages/Suave.Azure.Functions) is available now as a Nuget Package

## Summary

<blockquote class="twitter-tweet" data-lang="en"><p lang="en" dir="ltr">&quot;F# is a general purpose language, not just a science, or data science language.&quot; <a href="https://twitter.com/tomaspetricek">@tomaspetricek</a> <a href="https://twitter.com/hashtag/ndcoslo?src=hash">#ndcoslo</a> <a href="https://twitter.com/hashtag/fsharp?src=hash">#fsharp</a></p>&mdash; Bryan Hunter (@bryan_hunter) <a href="https://twitter.com/bryan_hunter/status/741164339747520514">June 10, 2016</a></blockquote>


<blockquote class="twitter-tweet" data-lang="en"><p lang="en" dir="ltr">*PLEASE* Microsoft, stop saying <a href="https://twitter.com/hashtag/fsharp?src=hash">#fsharp</a> is great for &quot;financial applications and stuff like that&quot;. It&#39;s a bloody general purpose language.</p>&mdash; Isaac Abraham (@isaac_abraham) <a href="https://twitter.com/isaac_abraham/status/740209486359605248">June 7, 2016</a></blockquote>

The complete source code is available in [my GitHub repository](https://github.com/tamizhvendan/TamAzureFun).

## Are you interested in learning more about F#?

I’m delighted to share that I’m running a tutorial at [Progressive F# Tutorials 2016](https://skillsmatter.com/conferences/7431-progressive-f-sharp-tutorials-2016), London on Dec 5, 2016. I'm excited to share my experiences with Suave and help developers to understand this wonderful F# library.

The Progressive F# Tutorials offer hands-on learning for every skill set and is led by some of the best experts in F# and functional programming






