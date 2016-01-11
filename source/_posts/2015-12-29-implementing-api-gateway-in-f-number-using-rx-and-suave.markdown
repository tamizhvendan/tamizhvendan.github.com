---
layout: post
title: "Implementing API Gateway in F# using Rx and Suave"
date: 2015-12-29 09:22:49 +0530
comments: true
categories:
  - fsharp
  - suave
  - Rx
---

One of the impressive aspects of functional programming is that it will enable you to write simple and expressive code to solve  complex problems. If you are wondering, how is it possible? Well, It is because, Functional Programming provides a lot of higher level abstractions to solve the problem in hand and avoids the need of writing lot of boilerplate & plumbing code. As a developer, you will be more productive and deliver the solution faster.

In this blog post, we are going to see the application of the above statements by implementing an [API Gateway Pattern](https://www.nginx.com/blog/building-microservices-using-an-api-gateway) in F# using [Rx](http://reactivex.io/) and [Suave](https://suave.io/).  

> This blog post is my contribution towards [fsharp advent calendar 2015](https://sergeytihon.wordpress.com/2015/10/25/f-advent-calendar-in-english-2015/). Do check out other great fsharp posts there

## API Gateway Pattern
Let's assume that you are asked to build the backend for the below GitHub user profile screen.

![Github Profile](http://res.cloudinary.com/tamizhvendan/image/upload/v1450769741/Profile.png)

This profile screen has 3 components

1. User - Username and Avatar

2. Popular Repos - Top 3 Repos of the Given User (determined by the number of stars)

3. Languages being used in the corresponding repos




Now as a developer, you have two options


* Build the Profile completely in client side by calling their corresponding GitHub APIs
  ![Profile with URLs](http://res.cloudinary.com/tamizhvendan/image/upload/v1450769816/Profile_With_API_Calls.png)

  This approach makes the client side complex as it involves five HTTP requests to create the complete profile screen. It has a lot of drawbacks as mentioned in [this blog post](http://techblog.netflix.com/2013/01/optimizing-netflix-api.html) by Netflix.


* Create a Facade API which takes care of the heavy lifting and hides the complexity of calling multiple services by  exposing a single endpoint which returns all the data required to create the profile screen in one API call
  ![API Gateway](http://res.cloudinary.com/tamizhvendan/image/upload/v1450775641/API_Gateway.png)

  This approach is called API Gateway. It is one of the commonly used [patterns in the microservices](http://microservices.io/patterns/apigateway.html) world.

## Rx

API Gateway is normally implemented using [Rx](http://reactivex.io/). Rx, an implementation of [Functional Reactive Programming](https://gist.github.com/staltz/868e7e9bc2a7b8c1f754), makes it easy to implement this kind of problems by enable us to think each asynchronous operation as streams (aka Observables). These streams offer a declarative API to work with the asynchronous operations.

In the implementation that we are going to see, we will be using [FSharp.Control.Reactive](http://fsprojects.github.io/FSharp.Control.Reactive/), an extension and wrapper for using the Reactive Extensions (Rx) with F#

## Project Structure

Create a new F# Console Application Project with the name `RxFsharp` and then create the files mentioned below in the given order

```
RxFsharp
|--Http.fs --> Abstraction to make http requests
|--GitHub.fs --> Define data types, transforming & parsing of responses from GitHub
|--ObservableExtensions.fs --> Some Util methods over Rx Observable
|--Profile.fs --> Implemention of API Gateway using Rx
|--ApiGateway.fs --> Suave API
|--Program.fs --> Entry Point
|--user.json --> Sample response of GitHub user API
|--repos.json --> Sample response of GitHub repos API
|--App.config
|--packages.config
```

Then install the following NuGet packages

* [FSharp.Control.Reactive](https://www.nuget.org/packages/FSharp.Control.Reactive)
* [Http.fs](https://www.nuget.org/packages/Http.fs)
* [Suave](https://www.nuget.org/packages/Suave)
* [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)
* [FSharp.Data](https://www.nuget.org/packages/FSharp.Data)


## Modeling Http Request & Response as Stream

In Functional Reactive Programming(FRP) every action is treated as stream. So, first step in implementing an API Gateway is modeling each HTTP request&response as stream.

Open `Http.fs` file and update it as below

```fsharp
module Http

open HttpClient
open FSharp.Control.Reactive

type HttpResponse =
| Ok of string
| Error of int

let getResponseAsync url =
  async {
    let! response =
      createRequest Get url
      |> withHeader (UserAgent "FsharpRx")
      |> HttpClient.getResponseAsync
    let httpResponse =
      match response.StatusCode with
      | 200 -> response.EntityBody.Value |> Ok
      | _ -> response.StatusCode |> Error
    return httpResponse
  }

let asyncResponseToObservable = getResponseAsync >> Observable.ofAsync
```

The `getResponseAsync` function makes use of [Http.fs](https://github.com/relentless/Http.fs), an HTTP Client library for F#, to fire the HTTP GET request and returns the HTTP response asynchronously.

The HTTP response has been modeled as either `Ok` with the response content for all the responses with the status code `200` and everything else is treated as `Error` for simplicity.

In the final line, we create a stream (aka Observable) from asynchronous response using the function `Observable.ofAsync` from [FSharp.Control.Reactive](https://github.com/fsprojects/FSharp.Control.Reactive)

As all the GitHub API [requests require user agent](https://developer.github.com/v3/#user-agent-required), we are adding one before firing the request.

## Parsing and Transforming GitHub API Responses

Upon successful response from GitHub, the next step is parsing the JSON response and transforming the parsed response as per the Profile screen requirements.

To parse the response, we are going to leverage the powerful tool of FSharp called [JsonTypeProvider](http://fsharp.github.io/FSharp.Data/library/JsonProvider.html)

The JSON Type Provider provides statically typed access to JSON documents. It takes a sample document as an input. Here in our case, it is user.json and repos.json

Get the sample response from GitHub using its [User API](https://api.github.com/users/tamizhvendan) and [Repos API](https://api.github.com/users/tamizhvendan/repos) and save the response in user.json and repos.json respectively.

With the help of JsonTypeProvider, we can now easily parse the raw JSON response to its equivalent types in just a few lines of code in `GitHub.fs`!

```fsharp
module GitHub

open Http
open FSharp.Data

type GitHubUser = JsonProvider<"user.json">
type GitHubUserRepos = JsonProvider<"repos.json">

let parseUser = GitHubUser.Parse
let parseUserRepos = GitHubUserRepos.Parse
```

As `GitHub.fs` is an abstraction of GitHub let's put their URLs also here in the same file

```fsharp
let host = "https://api.github.com"
let userUrl = sprintf "%s/users/%s" host
let reposUrl = sprintf "%s/users/%s/repos" host
let languagesUrl repoName userName = sprintf "%s/repos/%s/%s/languages" host userName repoName
```
We have used [partial application](http://fsharpforfunandprofit.com/posts/partial-application) of function `sprintf` in the `userUrl` and `reposUrl` function here, so both of these functions take username as its parameter implicitly

The JSON response of [languages](https://api.github.com/repos/tamizhvendan/fsharp-phonecat/languages) API is little tricky to parse as its keys are dynamic. The type provider will work only if the underlying response has a fixed schema. So, we can't use the type provider directly to parse the response of languages API.

We are going to use the inbuilt [JSON Parser](http://fsharp.github.io/FSharp.Data/library/JsonValue.html) available with JsonTypeProvider to parse the languages response

```fsharp
let parseLanguages languagesJson =
  languagesJson
  |> JsonValue.Parse
  |> JsonExtensions.Properties
  |> Array.map fst
```

The `JsonValue.Parse` parses the raw string JSON to a type called `JsonValue` and the `JsonExtensions.Properties` function takes a `JsonValue` and returns the key and value pairs of all the properties in the JSON as tuples. As we are interested only in the Keys here, we just pluck that value alone using the function `fst`

Great! Now we are done with parsing the JSON response of all the three APIs and creating it's equivalent types. The next step is doing some business transformation

One of the requirements of the Profile Screen is that we need to show only the top 3 popular repositories based on the stars received by the repository. Let's implement it

```fsharp
let popularRepos (repos : GitHubUserRepos.Root []) =
  let ownRepos = repos |> Array.filter (fun repo -> not repo.Fork)
  let takeCount = if ownRepos.Length > 3 then 3 else repos.Length

  ownRepos
  |> Array.sortBy (fun r -> -r.StargazersCount)
  |> Array.toSeq
  |> Seq.take takeCount
  |> Seq.toArray
```

In the `Http.fs` file, we defined how to get the raw JSON response using Stream as `HttpResponse` (`Ok` & `Error`) and in this `GitHub.fs`, we have defined how to parse this raw JSON response and transform them its equivalent strongly types.

The final step is integrating both these logic and return the Output record type needed by the Profile Screen

let's start by defining the Profile type

```fsharp
type Profile = {
  Name : string
  AvatarUrl : string
  PopularRepositories : Repository seq
} and Repository = {
  Name : string
  Stars : int
  Languages : string[]
}
```
Then write functions to get the value from `HttpResponse` and create this profile output.

```fsharp
let reposResponseToPopularRepos = function
  |Ok(r) -> r |> parseUserRepos |> popularRepos
  |_ -> [||]

// Languages always associated with a repository
let languageResponseToRepoWithLanguages (repo : GitHubUserRepos.Root) = function
  |Ok(l) -> {Name = repo.Name; Languages = (parseLanguages l); Stars = repo.StargazersCount}
  |_ -> {Name = repo.Name; Languages = Array.empty; Stars = repo.StargazersCount}

let toProfile  = function
  |Ok(u), repos ->
      let user = parseUser u
      {Name = user.Name; PopularRepositories = repos; AvatarUrl = user.AvatarUrl} |> Some
  | _ -> None
```

All the above functions except `toProfile` take `HttpResponse` as its last parameter implicitly.

In `reposResponseToPopularRepos` function, if the repos API request is successful, we parse the response to its equivalent type and then pick only three of them based on star count and in the case of an error we just return an empty array.

The `languageResponseToRepoWithLanguages` function handles the response from the Languages API request which takes its associated repository as its first parameter. If the response is successful, then it creates the `Repository` record with the returned languages else it just the `Repository` record with an empty array for languages.

The last function `toProfile` is a merge function which takes a tuple of `HttpResponse` (of User API request) and `Repository []` and creates a `Profile` record if the response is successful. In case of an error, it just returns `None`

*Note: To keep this blog post simple, I am handling the errors using empty arrays and None. It can be extended using [ROP](http://fsharpforfunandprofit.com/rop/)*

## Implementing API Gateway

Let me quickly summarize what we have done so far. We have created two abstractions.

* `Http` - Responsible for firing the HTTP GET request with the given URL and give the response as a Rx Stream

```
URL
  \
----------Response--|

```

* `GitHub` - Takes care of parsing the JSON response from GitHub API and does some business logic (finding top 3 popular repositories). Then returns the output in the format that the Client needs.

With the help of these abstractions now we are going to build the API Gateway to get the profile object.

Open `Gateway.fs` and add the `getProfile` function

```fsharp
//string -> Async<Profile>
let getProfile username =
  async {
    // TODO
    return! profile
  }
```
It is just a skeleton which just describes the what function does. Let's start its implementation.

In Rx world, everything is a stream. So, the first step is converting GitHub User API request to stream

```fsharp
let getProfile username =
  // ...
  let userStream = username |> userUrl |> asyncResponseToObservable
  // ...
```


We have created the `userStream` using the `userUrl` and `asyncResponseToObservable` function defined earlier.

```
userURL
  \
-----------UJ--|              // UJ - GitHub User Response in JSON format
```

Then we need top three popular repositories as a stream

```fsharp
let getProfile username =
  // ...
  let popularReposStream =
    username
    |> reposUrl
    |> asyncResponseToObservable
    |> Observable.map reposResponseToPopularRepos
  // ...
```

Except the last line, everything is same as that of creating `userStream`. If you check [GitHub repos API](https://api.github.com/users/tamizhvendan/repos) it just returns all the repos associated with the given username. But what we need is only top three of them based on the number of stars it has received.

We already have a function `reposResponseToPopularRepos` in `GitHub.fs` which does the job of picking the top three repos from the raw JSON. As the response is in the form Rx stream, we need to get the value from the stream and then we need to apply this function and that's what the `Observable.map` does. It is very similar to [Array.map](https://developer.mozilla.org/en/docs/Web/JavaScript/Reference/Global_Objects/Array/map) and [LINQ Select](https://msdn.microsoft.com/en-us/library/system.linq.enumerable.select(v=vs.100).aspx)

```
reposURL
  \
----------RJ---|                          // RJ - GitHub user repos in JSON format
          | MAP function (RJ -> PR)       // PR - Popular Repos
          V
--------------PR---|
```

The next operation is a little complex. i.e. finding out the programming languages being used in these popular repositories. To get this GitHub API, we need to fire three separate requests to the [Languages API](https://developer.github.com/v3/repos/#list-languages) for each repository and then merge the results back with the corresponding repository.

To implement this with the help of Rx streams, we need to understand the `flatMap` [function of Rx](http://reactivex.io/documentation/operators/flatmap.html).

It is similar to the `map` function that we have seen before with a difference that it takes a function that returns a new item stream instead of a new item.

### Map

![Map](http://reactivex.io/documentation/operators/images/map.c.png)

The `map` function has the following signature

```
('a -> 'b) -> IObservable<'a> -> IObservable<'b>
```

### FlatMap

![FlatMap](http://reactivex.io/documentation/operators/images/flatMap.c.png)

The `flatMap` function has the following signature

```
('a -> IObservable<'b>) -> IObservable<'a> -> IObservable<'b>
```

This function is also called as `bind` function in the functional programming world. If you would like to know further on this topic, I strongly recommend [this blog series](http://fsharpforfunandprofit.com/posts/elevated-world) by the fsharp great, [Scott Wlaschin](https://twitter.com/ScottWlaschin)

Back to the problem in hand, we need to fire three HTTP GET Requests to get back the languages associated with the each of the top three popular repos. In terms of the `flatMap` function, it boils down to three functions of the following syntax

```
(GitHubUserRepos.Root -> IObservable<Repostiory>)
  -> IObservable<GitHubUserRepos.Root> -> IObservable<Repostiory>
```

We can implement the solution using three `flatMap` functions using the above syntax. But, we can make it more granular by creating a new variant of `flatMap` function to achieve this in a more straightforward way.

The ideal function that we are looking for in this `flatMap` variant holds the following signature

```
('a -> IObservable<'b>) -> IObservable<'a []> -> IObservable<'b []>
```
i.e three `flatMap` can be rewritten as

```
(GitHubUserRepos.Root -> IObservable<Repostiory>)
  -> IObservable<GitHubUserRepos.Root []> -> IObservable<Repostiory []>
```

Let's name it as `flatMap2` and add the implementation of it in the file `ObservableExtensions.fs`

```fsharp
module ObservableExtensions
open FSharp.Control.Reactive

let flatmap2 f observable =
  observable
  |> Observable.flatmap (Array.map f >> Observable.mergeArray)
  |> Observable.toArray
```

Here is the representation of what this function does

```
-----X[n]--------------------------|->
      \   flatMap on each item in X which yield the stream of R
-------R1-------------------------|->
----------R2----------------------|->
          ......
-------------Rn-------------------|->
          ||  mergeArray -> merge array of R streams into one stream
          VV
------R1-R2--Rn-------------------|->
              \ toArray -> Creates an array from an observable sequence
-------------R[n]-----------------|->
```

It's hard to get it right in a single shot. So, let's see it in detail step by step by applying it to our use case here

* We have the stream of three popular repos (i.e array of repos)

```
-----X[n]--------------------------|->
```
* To get the languages associated with the each repo we need to call the languages API for every repo item in the above step

```
-----X[n]--------------------------|->
      \   flatMap on each item in X which yield the n streams of R
-------R1-------------------------|->
----------R2----------------------|->
          ......
-------------Rn-------------------|->
```
* After we received the response from each languages API call, we need to merge them into one stream

```
-------R1-------------------------|->
----------R2----------------------|->
          ......
-------------Rn-------------------|->
          |  mergeArray -> merge array of R streams into one stream
          V
------R1-R2--Rn-------------------|->
```

* To integrate this with the expected application response, we need all the responses in a single go

```
------R1-R2--Rn-------------------|->
              \ toArray -> Creates an array from an observable sequence
-------------R[n]-----------------|->
```

Great! You got it right!!

Let's use this `flatMap2` function and complete the implementation of profile API gateway.

```fsharp
let getProfile username =
  // ...
  let toRepoWithLanguagesStream (repo : GitHubUserRepos.Root) =    
    username
    |> languagesUrl repo.Name
    |> asyncResponseToObservable
    |> Observable.map (languageResponseToRepoWithLanguages repo)

  let popularReposStream =
    username
    |> reposUrl
    |> asyncResponseToObservable
    |> Observable.map reposResponseToPopularRepos
    |> flatmap2 toRepoWithLanguagesStream

  // ...
```

The `flatmap2` function takes the function `toRepoWithLanguagesStream` which converts the `GitHubUserRepos.Root` type to `IObservable<Repository>` to find out the languages associated with the given popular repositories.

The `toRepoWithLanguagesStream` function does the following

```
GitHubUserRepos.Root
  \ create a languages GitHub API stream using the repo name from the input
-------LR----------|              // LR - languages GitHub API response
        \ MAP function (GitHubUserRepos.Root -> LR -> R)
------------R------|              // R - Repository type that defined earlier
```
The `Observable.map` function takes only one input, but here we need to two inputs. So, With the help of [partial application](http://fsharpforfunandprofit.com/posts/partial-application/), we created an intermediate function by partially applying the first parameter alone

```fsharp
Observable.map (languageResponseToRepoWithLanguages repo)
```

The `languageResponseToRepoWithLanguages` function has been already defined in the `GitHub.fs` file.

The last step of the `getProfile` function is combining this `popularReposStream` with the `userStream` created earlier and return the `Profile` type asynchronously.

```fsharp
let getProfile username =
  // ...
  async {
    return! popularReposStream
            |> Observable.zip userStream
            |> Observable.map toProfile
            |> TaskObservableExtensions.ToTask
            |> Async.AwaitTask
  }
  // ...
```

The `Observable.zip` function takes two streams as its input, then merges the output of each stream and return the output as a tuple. From this tuple, we have used `Observable.map` function to map it a `Profile` type using the `toProfile` function created earlier in `GitHub.fs`

```
-----UR-------------|     // UR - GitHub User API Response
--------PR----------|     // PR - Popular Repos
        \ ZIP function (UR -> PR -> (UR,PR))   
---------(UR,PR)----|
          \ MAP ( (UR,PR) -> P )  // P - Profile
----------P---------|
```

The last functions `TaskObservableExtensions.ToTask` and `Async.AwaitTask` does the conversion of `IObservable` to `async` by converting it to a `Task` first and then the `Task` to `async`

The final `getProfile` function will be like

```fsharp

let getProfile username =

  let userStream = username |> userUrl |> asyncResponseToObservable

  let toRepoWithLanguagesStream (repo : GitHubUserRepos.Root) =    
    username
    |> languagesUrl repo.Name
    |> asyncResponseToObservable
    |> Observable.map (languageResponseToRepoWithLanguages repo)

  let popularReposStream =
    username
    |> reposUrl
    |> asyncResponseToObservable
    |> Observable.map reposResponseToPopularRepos
    |> flatmap2 toRepoWithLanguagesStream

  async {
    return! popularReposStream
            |> Observable.zip userStream
            |> Observable.map toProfile
            |> TaskObservableExtensions.ToTask
            |> Async.AwaitTask
  }
```

This function is a testimonial on *How functional programming helps to write less, robust, and readable code to solve a complex problem*

We have handled all the five HTTP requests asynchronously, did some error handling (by returning empty types), and finally efficiently combined outputs of these five HTTP requests and created a type to send back to the client. Everything is asynchronous!

Pretty awesome isn't it?

## Exposing the API

The final step is exposing what we have done so far as an API to the outside world. We are going to implement this using [Suave](https://suave.io/)

Open `ApiGateway.fs` file and update it as below

```fsharp
let JSON v =  
  let jsonSerializerSettings = new JsonSerializerSettings()
  jsonSerializerSettings.ContractResolver <- new CamelCasePropertyNamesContractResolver()

  JsonConvert.SerializeObject(v, jsonSerializerSettings)
  |> OK
  >=> Writers.setMimeType "application/json; charset=utf-8"

let getProfile userName (httpContext : HttpContext) =
   async {
      let! profile = getProfile userName
      match profile with
      | Some p -> return! JSON p httpContext
      | None -> return! NOT_FOUND (sprintf "Username %s not found" userName) httpContext
   }
```

The `JSON` is a utility function (WebPart in the world of Suave) which takes any type, serialize it to JSON format and return it as JSON HTTP response.

The `getProfile` function is the API WebPart which calls our backend API gateway implementation and pass the received response to the `JSON` WebPart defined before.

In case if there is no profile available (Remember? we return empty types in case of errors), we just return `404` with the message that the given username is not found.

Then update the `Program.fs` to write the web server code

```fsharp
[<EntryPoint>]
let main argv =
  let webpart = pathScan "/api/profile/%s" ApiGateway.getProfile
  startWebServer defaultConfig webpart
  0
```

Thanks to `Suave` for it's lightweight and low-ceremony offerings in creating an API. We just exposed it in two lines!

Hit `F5` and access the API at `http://localhost:8083/api/profile/{github-username}` Bingo!!

## Summary

> A language that doesn’t affect the way you think about programming is not worth knowing - Alan Perils

The above quote summarizes the gist of this blog post. Functional Programming will help you think better. You can get the source code associated with the blog post in my [blog-samples GitHub repository](https://github.com/tamizhvendan/blog-samples/tree/master/RxFsharp)

Wish you a happy and prosperous new year
