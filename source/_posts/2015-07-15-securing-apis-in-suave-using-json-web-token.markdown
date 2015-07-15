---
layout: post
title: "Securing APIs in Suave using JSON Web Token"
date: 2015-07-15 12:04:12 +0530
comments: true
categories: 
  - fsharp  
  - suave
---

In the [last blog post]({% post_url 2015-06-11-building-rest-api-in-fsharp-using-suave%}), we have seen how can we combine small functions to create a REST API in Suave and in this blog post we are going to see how can we secure the APIs using JSON web tokens(JWT). 

This blog post is based on [Taiseer's](http://bitoftech.net/) blog post on [JSON Web Token in ASP.NET Web API 2 using Owin](http://bitoftech.net/2014/10/27/json-web-token-asp-net-web-api-2-jwt-owin-authorization-server/) and I will be covering how to implement the same in [Suave](http://suave.io). If you are interested in the theoretical background, kindly read his blog post before reading this.

## Workflow

The typical workflow of JWT based application would look like this

1. New Audience (Resource Server) gets registered with Authorization Server.
2. Get the JWT access token from Authorization Server by passing Client Id of the resource server and login credentials
3. Use the access token obtained above to get access to the secured resources in the resource server

We are going to see all these three steps in this blog post


## Project Setup

Create an empty visual studio solution and add new projects with the following name

* SuaveJwt - A fsharp library project which contains all the JWT related things
* SuaveJwt.AuthServerHost - A fsharp console application which is going to host the authorization server
* Audience1 - A fsharp console application representing the resource server 1   
* Audience2 - A fsharp console application representing the resource server 2   

After creating, install the following nuget packages

* [Suave](https://www.nuget.org/packages/Suave/) in all the four projects
* [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/) and [JSON Web Token Handler](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt/) in *SuaveJwt*

Then add reference the .NET framework library **System.IdentityModel** in all the four projects from GAC.

{% img center /images/suave_jwt/project_structure.png %}

## New Audience Registration

As we seen in the workflow, the first step is enable an audience to register itself with the authorization server. Let's begin by defining the business logic.

Add a new source file *JwtToken.fs* in the **SuaveJwt** project and update it as follows

```fsharp
type Audience = {
  ClientId : string
  Secret : Base64String
  Name : string
}

// string -> Audience
let createAudience audienceName =
  let clientId = Guid.NewGuid().ToString("N")
  let data = Array.zeroCreate 32
  RNGCryptoServiceProvider.Create().GetBytes(data)
  let secret = data |> Base64String.create 
  {ClientId = clientId; Secret = secret; Name =  audienceName} 
``` 

The above snippet create an audience record with a random client id and secret key. The secret key is of type [base64 url encoded](https://en.wikipedia.org/wiki/Base64#URL_applications) string. This type is not exist, so let's create it.

Add a new source file *Encodings.fs* in the **SuaveJwt** project and add this type

```fsharp
type Base64String = private Base64String of string with
        
  static member decode (base64String : Base64String) = 
    let (Base64String text) = base64String
    let pad text =
      let padding = 3 - ((String.length text + 3) % 4)
      if padding = 0 then text else (text + new String('=', padding))
    
    Convert.FromBase64String(pad(text.Replace('-', '+').Replace('_', '/')))

  static member create data =
    Convert.ToBase64String(data)
      .TrimEnd('=')
      .Replace('+', '-')
      .Replace('/', '_') |> Base64String;

     
  static member fromString = Base64String

  override this.ToString() = 
    let (Base64String str) = this
    str
```  

To keep things simple I haven't added any validations here. The next step is create a Suave *WebPart* to expose the audience create functionality.

Add a new source file *AuthServer.fs* in the *SuaveJwt* project and update it as below

```fsharp
type AudienceCreateRequest = {
  Name : string
}

type AudienceCreateResponse = {
  ClientId : string
  Base64Secret : string
  Name : string
}

type Config = {
  AddAudienceUrlPath : string  
  SaveAudience : Audience -> Async<Audience>
}

let audienceWebPart config =
  
  let toAudienceCreateResponse (audience : Audience) = {
    Base64Secret = audience.Secret.ToString()
    ClientId = audience.ClientId        
    Name = audience.Name
  }

  let tryCreateAudience (ctx: HttpContext) =
    match mapJsonPayload<AudienceCreateRequest> ctx.request with
    | Some audienceCreateRequest -> 
        async {
          let! audience = audienceCreateRequest.Name |> createAudience |> config.SaveAudience                     
          let audienceCreateResponse = toAudienceCreateResponse audience
          return! JSON audienceCreateResponse ctx
        }
    | None -> BAD_REQUEST "Invalid Audience Create Request" ctx

  path config.AddAudienceUrlPath >>= POST >>= tryCreateAudience 
```

The ```audienceWebPar``` retrieves the ```AudienceCreateRequest``` from the json request payload and creates a new audience. 
To make it independent of the host, we have externalized the hosting functionality using the ```Config``` record type.

The ```mapJsonPayload``` and ```JSON``` functions serialize and deserialize json objects across the wire respectively. These functions are not part of Suave, so let's add them

Add a new source file *SuaveJson.fs* in the **SuaveJwt** project and add these functions

```fsharp
let JSON v =     
  let jsonSerializerSettings = new JsonSerializerSettings()
  jsonSerializerSettings.ContractResolver <- new CamelCasePropertyNamesContractResolver()
  
  JsonConvert.SerializeObject(v, jsonSerializerSettings)
  |> OK 
  >>= Writers.setMimeType "application/json; charset=utf-8"

let mapJsonPayload<'a> (req : HttpRequest) =     
  let fromJson json =
    try 
      let obj = JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a    
      Some obj
    with
    | _ -> None

  let getString rawForm = 
    System.Text.Encoding.UTF8.GetString(rawForm)

  req.rawForm |> getString |> fromJson
```  

The next step is hosting this audience web part.

Open the *Program.cs* file in the **SuaveJwt.AuthServerHost** project and update it as below

```fsharp
let authorizationServerConfig = {
  AddAudienceUrlPath = "/api/audience"  
  SaveAudience = AudienceStorage.saveAudience
}
let audienceWebPart' = audienceWebPart authorizationServerConfig
startWebServer defaultConfig audienceWebPart'
0 // return an integer exit code
```

It is a straight forward self-host suave server program which exposes the audience web part.

The *AudienceStorage* is responsible for storing the created audiences and to add it create a new source file *AudienceStorage.fs* in the **SuaveJwt.AuthServerHost** project and add these functions 

```fsharp
module AudienceStorage

let private audienceStorage 
  = new Dictionary<string, Audience>()

let saveAudience (audience : Audience) =      
    audienceStorage.Add(audience.ClientId, audience)
    audience |> async.Return
```
To keep this simple, we are using in-memory dictionary here and it can be easily replaced with any data store

Now we have everything to create a new audience. So, let's run the **SuaveJwt.AuthServerHost** application and verify

{% img center /images/suave_jwt/audience_create.png %}