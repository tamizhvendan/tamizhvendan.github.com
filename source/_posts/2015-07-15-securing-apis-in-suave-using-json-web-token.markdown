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

Then add reference the .NET framework library **System.IdentityModel** in all the four projects from GAC and then add reference to the **SuaveJwt** project library in all the other three projects.

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

Add a new source file *AuthServer.fs* in the **SuaveJwt** project and update it as below

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
          let! audience = 
            audienceCreateRequest.Name |> createAudience |> config.SaveAudience                     
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

Open the *Program.fs* file in the **SuaveJwt.AuthServerHost** project and update it as below

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

Keep a note of this *clientId* and *base64Secret* as we will be using them while defining the resource server ("Audience1" in this case).

## Generating Access Token

After registering an audience with the authorization server, the next step is to get the access token to access the resources in the given resource server.

Let's begin by defining the business logic to create an access token.

{% img center /images/suave_jwt/createToken.png %}

Open *JwtToken.fs* and add the the following types

```fsharp
type TokenCreateRequest = {         
  Issuer : string        
  UserName : string
  Password : string        
  TokenTimeSpan : TimeSpan
}

type IdentityStore = {
  getClaims : string -> Async<Claim seq>
  isValidCredentials : string -> string -> Async<bool>
  getSecurityKey : Base64String -> SecurityKey
  getSigningCredentials : SecurityKey -> SigningCredentials
}

type Token = {
  AccessToken : string        
  ExpiresIn : float        
} 
```

These types are generic abstractions which decouple the token creation part from the underlying host. 

The ```TokenCreateRequest``` models the underlying issuer and token lifetime.

The ```IdentityStore``` represents a generic datastore in which the identity information has been stored and it also provides the security key and the signing credentials to protect the access token from misuse.

With these types in place let's add the ```createToken``` function in the *SuaveJwt.fs* file

```fsharp
let createToken tokenCreateRequest identityStore audience = 
  async {
    let! isValidCredentials = 
      identityStore.isValidCredentials tokenCreateRequest.UserName tokenCreateRequest.Password
    if isValidCredentials then                            
      let signingCredentials = 
        (identityStore.getSecurityKey >> identityStore.getSigningCredentials) audience.Secret
      let issuedOn = Nullable DateTime.UtcNow
      let expiresBy = Nullable (DateTime.UtcNow.Add(tokenCreateRequest.TokenTimeSpan))       
      let! claims =  identityStore.getClaims tokenCreateRequest.UserName 
      let jwtSecurityToken = 
        new JwtSecurityToken(tokenCreateRequest.Issuer, audience.ClientId, claims, issuedOn, expiresBy, signingCredentials)
      let handler = new JwtSecurityTokenHandler()
      let accessToken = handler.WriteToken(jwtSecurityToken)                
      return Some {AccessToken = accessToken; ExpiresIn = tokenCreateRequest.TokenTimeSpan.TotalSeconds}
    else return None 
  }
```

The ```createToken``` function checks the validness of the login credentials. If it is valid, then it get the claims associated with the given username and creates a *JwtToken* using the [JSON Web Token Handler](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt/) library, else it returns ```None```

Now we have got the backend business logic ready, let's expose it as a suave *WebPart*

Open *AuthServer.fs* and add a new record type for the incoming token create request and update ```Config``` and ```audienceWebPart``` as below

```fsharp
type TokenCreateCredentials = {
  UserName : string
  Password : string
  ClientId : string
}

type Config = {
  // ... existing fields ...
  CreateTokenUrlPath : string  
  GetAudience : string -> Async<Audience option>
  Issuer : string
  TokenTimeSpan : TimeSpan
}

let audienceWebPart config identityStore =
  
  // ... existing functions ...

  let tryCreateToken (ctx: HttpContext) =
    match mapJsonPayload<TokenCreateCredentials> ctx.request with
    | Some tokenCreateCredentials -> 
      async {
        let! audience = config.GetAudience tokenCreateCredentials.ClientId
        match audience with
        | Some audience ->
            let tokenCreateRequest : TokenCreateRequest = {         
              Issuer = config.Issuer        
              UserName = tokenCreateCredentials.UserName
              Password = tokenCreateCredentials.Password        
              TokenTimeSpan = config.TokenTimeSpan
            }            
            let! token = createToken tokenCreateRequest identityStore audience
            match token with
            | Some token -> return! JSON token ctx
            | None -> return! BAD_REQUEST "Invalid Login Credentials" ctx
            
        | None -> return! BAD_REQUEST "Invalid Client Id" ctx
      }    
    | None -> BAD_REQUEST "Invalid Token Create Request" ctx

  choose [
    path config.AddAudienceUrlPath >>= POST >>= tryCreateAudience
    path config.CreateTokenUrlPath >>= POST >>= tryCreateToken
  ]
```

The ```tryCreateToken``` function checks whether the ```ClientId``` is a registered audience or not, if it exists, then this function creates a token using the ```createToken``` function defined above otherwise it returns the ```BAD_REQUST``` *WebPart* with the appropriate error message.

The final change is modifying the *Program.fs* and *AudienceStorage.fs* files in the **SuaveJwt.AuthServerHost** to expose this *WebPart*.

**Program.fs**

```fsharp
let authorizationServerConfig = {
  AddAudienceUrlPath = "/api/audience"
  CreateTokenUrlPath = "/oauth2/token"
  SaveAudience = AudienceStorage.saveAudience
  GetAudience = AudienceStorage.getAudience
  Issuer = "http://localhost:8083/suave"
  TokenTimeSpan = TimeSpan.FromMinutes(1.)
}

let identityStore = {
  getClaims = IdentityStore.getClaims
  isValidCredentials = IdentityStore.isValidCredentials
  getSecurityKey = KeyStore.securityKey
  getSigningCredentials = KeyStore.hmacSha256
}    

let audienceWebPart' = audienceWebPart authorizationServerConfig identityStore   

startWebServer defaultConfig audienceWebPart'
```

**AudienceStorage.fs**

```fsharp
let getAudience clientId =        
  if audienceStorage.ContainsKey(clientId) then 
    Some audienceStorage.[clientId] |> async.Return
  else
    None |> async.Return
```

The *KeyStore* and *IdentityStore* are not exists, so let's add them

Add *KeyStore.fs* in the **SuaveJwt** project and it provide the in-memory symmetric security key based on [HMAC](https://en.wikipedia.org/wiki/Hash-based_message_authentication_code)

```fsharp
module KeyStore

open System.IdentityModel.Tokens
open Encodings

let securityKey sharedKey : SecurityKey =
  let symmetricKey = sharedKey |> Base64String.decode
  new InMemorySymmetricSecurityKey(symmetricKey) :> SecurityKey

let hmacSha256 secretKey =
  new SigningCredentials(secretKey,SecurityAlgorithms.HmacSha256Signature, SecurityAlgorithms.Sha256Digest)
```  

Then add *IdentityStore.fs* in the **SuaveJwt.AuthServerHost** project.

```fsharp
module IdentityStore    

open System.Security.Claims       

let getClaims userName =
  seq {
    yield (ClaimTypes.Name, userName)
    if (userName = "Admin") then
      yield (ClaimTypes.Role, "Admin")
    if (userName = "Foo") then
      yield (ClaimTypes.Role, "SuperUser")            
  } |> Seq.map (fun x -> new Claim(fst x, snd x)) |> async.Return

let isValidCredentials username password =
  username = password |> async.Return
```

To keep this simple, I am just hard coding the credentials and claims here and it can be replaced with any backend. In this case, I am just going with accepting all the credentials as valid if the username and password are same. 

Let's run the **SuaveJwt.AuthServerHost** and verify the token

{% img center /images/suave_jwt/audience_admin_token.png %}

## Securing the resources

Now we have come to the interesting part of securing the resources using the *access token* created in the above step