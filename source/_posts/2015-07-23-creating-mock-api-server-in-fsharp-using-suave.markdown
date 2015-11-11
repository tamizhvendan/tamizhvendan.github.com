---
layout: post
title: "Creating Mock API server in fsharp using Suave"
date: 2015-07-23 16:56:20 +0530
comments: true
categories: 
  - fsharp
  - suave
---

As part of the current assignment in my day job, I am working in a web application which integrates with an another web application via web APIs. The backend calls the exposed web APIs and does some business logic. Due to some technical limitations, we are not able to setup a development environment of the Web APIs so we have decided to create a mock API server during the development and replace it with the real one in the production.

Since it's just a mock API server, we don't want to spend much time on it. Thanks to the awesome light-weight fsharp library [Suave](http://suave.io/) we have made it in just 15 minutes! 

In this blog post, I will be sharing how we have achieved it. As a sample, we will mock the [GitHub API](https://developer.GitHub.com/v3/).

## Getting Started

Create a new fsharp console application project "GitHubMockApiServer" in Visual Studio and install the [suave](https://www.nuget.org/packages/Suave/) nuget package.

To keep it short we are going to mock only two GitHub APIs

* [Get a single user](https://developer.GitHub.com/v3/users/#get-a-single-user) - **/users/:username** 
* [List user repositories](https://developer.GitHub.com/v3/repos/#list-user-repositories) - **/users/:username/repos**

## Creating the mock APIs

The first step is getting the sample response for these APIs and save them in separate JSON files. In this example, I've created these using my GitHub username and added to the project as below.

{% img center /images/suave_mock_api/project.png %}

After adding, change the "Copy to Output Directory" property of the both the files to "Copy always"

Open the **Program.fs** and update it as follows

```fsharp
open System.IO
open Suave.Http
open Suave.Http.Successful
open Suave
open Suave.Http.Applicatives
open Suave.Web

[<EntryPoint>]
let main argv = 
  let json fileName =
    let content = File.ReadAllText fileName  
    content.Replace("\r", "").Replace("\n","")
    |> OK >>= Writers.setMimeType "application/json"      
  
  let user = pathScan "/users/%s" (fun _ -> "User.json" |> json)  
  let repos = pathScan "/users/%s/repos" (fun _ -> "Repos.json" |> json)
  let mockApi = choose [repos;user]

  startWebServer defaultConfig mockApi          
  0
```

Short and Sweet! Now you can hit this mock GitHub APIs without worrying about its [rate limits](https://developer.GitHub.com/v3/#rate-limiting)

The ```json``` function reads the sample JSON file, then removes the line breaks and return the HTTP 200 response with the content type as  "application/json"

The nice DSL in suave enabled us to configure the routes without giving much works to our hands :-)  

## Mock APIs in action

{% img border center /images/suave_mock_api/user_api.png %}

{% img border center /images/suave_mock_api/repos_api.png %}

## Summary

This work is inspired by Scott Wlaschin's blog post series on [Low-risk ways to use F# at work](http://fsharpforfunandprofit.com/series/low-risk-ways-to-use-fsharp-at-work.html). I am glad to find one more low-risk way to use F# at work and I believe it would help you in future. You can find the sample code in my [blog-samples GitHub repository](https://github.com/tamizhvendan/blog-samples/tree/master/GithubMockApiServer).