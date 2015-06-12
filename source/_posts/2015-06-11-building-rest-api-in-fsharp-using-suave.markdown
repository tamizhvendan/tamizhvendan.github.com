---
layout: post
title: "Building REST Api in fsharp using Suave"
date: 2015-06-11 10:48:20 +0530
comments: true
categories: 
  - fsharp
  - suave
---

In the last one month lot of great things happening in fsharp world around [suave](suave.io), a simple web development fsharp library. Scott Hanselman [blogged](http://www.hanselman.com/blog/RunningSuaveioAndFWithFAKEInAzureWebAppsWithGitAndTheDeployButton.aspx) about it, [Tomas Petricek](https://twitter.com/tomaspetricek) kick-started an awesome [hands on session](https://skillsmatter.com/skillscasts/6381-suave-hands-on-with-tomas-petricek) and followed up with a [great talk](http://channel9.msdn.com/Blogs/Seth-Juarez/Deploying-an-F-Web-Application-with-Suave) on Channel 9. Last but not the least, [Tomasz Heimowski](https://twitter.com/theimowski) authored a clear and crisp [book on Suave](http://theimowski.gitbooks.io/suave-music-store/content/)

I got super excited after learning suave from these resources and started playing with it. One of the great things about the fsharp community is, if you want to improve any existing library all you need is just send a pull request with the feature you would like to have. Yes, it's as simple as that! It [worked](https://github.com/SuaveIO/suave/pull/259) for me and I am sure for you too, if you wish.

In this blog post, you are going to learn how to build a REST api using suave. The REST api that we are going to create here follows the standard being used in the [StrongLoop](http://docs.strongloop.com/display/public/LB/Use+API+Explorer), a node.js REST api library. To keep things simple, we are not going to see validation and error handling as part of this post and I will be covering that in an another post. 

Let's get started

## Setting up the project

Create a new "F# Console Application" project in Visual Studio with the name ```SuaveRestApi``` and rename ```Program.fs``` to ```App.fs```. This file is going to contain the application bootstrap logic. Add two more files ```Db.fs``` and ```RestFul.fs``` which would contain database access and restful api implementation code respectively. Ensure these files are in the order as shown in below. 

{% img center /images/sauve_rest_api/proj_structure.png %}

After creating, install the following nuget packages

* [Suave](https://www.nuget.org/packages/suave)
* [Newtonsoft.Json]()

## WebPart

The basic building block of Suave is [WebPart](http://theimowski.gitbooks.io/suave-music-store/content/webpart.html). It is an alias of function type ```HttpContext -> Async<HttpContext option>```. This simple function type actually model whole set of Request and Response model of Http protocol. From the Tomasz Heimowski's book here is the definition of the ```WebPart```

> Based on the http context, we give you a promise (async) of optional resulting http context, where the resulting context is likely to have its response set with regards to the logic of the WebPart itself

A ```WebPart``` represents a http request and response. Since it is a function, we can combine multiple ```WebPart```s together and build a complex web application which handles multiple requests and responses. This is the beauty of functional programming. You just think in terms of one function at a time and make it work. Once you are done, all you need to do is just gluing them together. Solving problems using this functional abstraction will make things much easier and help you to write less code compare to its Object Oriented version.


The ```OK``` webpart is a very simple one in Suave which takes a ```string``` and returns a http response with the status code ```200``` and the given string. Add the following code in the ```App.fs``` and run the console app.

```fsharp
open Suave.Web
open Suave.Http.Successful

[<EntryPoint>]
let main argv = 
  startWebServer defaultConfig (OK "Hello, Suave!") 
  0
```
The is the simplest Suave application that greets all visitors with the string "Hello, Suave!". The ```startWebServer``` is a blocking function that takes a configuration (port number, ssl, etc.,) and starts a http web server in the port 8083 upon calling.

{% img center border /images/sauve_rest_api/hello_suave.png %}

The Rest Api that we are going to design here is a ```WebPart``` which will be replacing this ```OK``` webpart.


## HTTP GET

Let's begin by defining a record type representing a *restful* resource. Open ```Restful.fs``` and update it as below

```fsharp
namespace SuaveRestApi.Rest
[<AutoOpen>]
module RestFul =  
  type RestResource<'a> = {
    GetAll : unit -> 'a seq        
  }
```

This ```RestResource``` is a container representing all the operations on a restful  resource. This record type abstracts the actual resource from the rest api web part. This enables the reuse of rest api web part across multiple resources.

Let's create a function in ```RestFul.fs``` which takes a resource name and this ```RestResource``` and returns a web part representing the restful api.

```fsharp
// string -> RestResource<'a> -> WebPart
let rest resourceName resource =
  // TODO
```

We are going to use the following building blocks of the suave library to implement the ```rest``` function.

* The ```path``` is a function of type: ```string -> WebPart```. It means that if we give it a string it will return ```WebPart```. Under the hood, the function looks at the incoming request and returns ```Some``` if the paths match, and ```None``` otherwise. 

* The ```GET``` is a static inbuilt ```WebPart``` which matches the HTTP GET requests.

* The ```>>=``` operator composes two WebParts into one by first evaluating the ```WebPart``` on the left, and applying the WebPart on the right only if the first one returned ```Some```.

To return a json response we need to change the mime type to "application/json" of the response. There is an [inbuilt function](https://github.com/SuaveIO/suave/blob/master/src/Suave/Json.fs) in suave to do this but it is using .Net's ```DataContractJsonSerializer```. I feel it's not ideal to use as we need to write decorator attribute ```DataMember``` to serialize the types.

As [Newtonsoft.Json](http://james.newtonking.com/archive/2014/04/30/json-net-6-0-release-3-serialize-all-the-f) provides serialization support for fsharp types without any need of decorating attributes, we will be using them there.

```fsharp
// 'a -> WebPart
let JSON v =     
  let jsonSerializerSettings = new JsonSerializerSettings()
  jsonSerializerSettings.ContractResolver <- new CamelCasePropertyNamesContractResolver()

  JsonConvert.SerializeObject(v, jsonSerializerSettings)
  |> OK 
  >>= Writers.setMimeType "application/json; charset=utf-8"
```
As the signature indicates ```JSON``` function takes a generic type, serialize it using *Newtonsoft.Json* and return the json response. ```Writers.setMimeType``` takes a mime type and a web part returns the web part with the given mime type.

Now we have everything to implement the HTTP GET request

```fsharp
let rest resourceName resource =
  let resourcePath = "/" + resourceName
  let gellAll = resource.GetAll () |> JSON
  path resourcePath >>= GET >>= getAll
```

The one caveat here is the path resolution of Suave library. Based on the webpart given, the ```startWebServer``` function configures it's internal routing table during the application startup and it is static after the application has been started. So, the ```getAll``` webpart will be created only during application startup. To avoid this, we need to wrap the ```getAll``` webpart with a ```warbler``` function which ensures that it is called only when the incoming request matches the given resource path.

```fsharp
let rest resourceName resource =
  let resourcePath = "/" + resourceName
  let gellAll = warbler (fun _ -> resource.GetAll () |> JSON)
  path resourcePath >>= GET >>= getAll
```  

Now we have the middleware to create a web part. Let's create other things and glue them together.

Open ```Db.fs``` file and add the following code

```fsharp
namespace SuaveRestApi.Db
open System.Collections.Generic
type Person = {
  Id : int
  Name : string
  Age : int
  Email : string
}
module Db =        
  let private peopleStorage = new Dictionary<int, Person>()
  let getPeople () = 
    peopleStorage.Values |> Seq.map (fun p -> p)
```

Since it's a sample application, I am just using an in memory dictionary to store the details of people. You can easily replace this with any data source. The ```Person``` type represents the ```People``` resource of the rest api.

The next step is wiring the Restful web part with the application. Open ```App.fs``` and update it as below

```fsharp
open SuaveRestApi.Rest
open SuaveRestApi.Db
open Suave.Web
open Suave.Http.Successful

[<EntryPoint>]
let main argv = 
  let personWebPart = rest "people" {
    GetAll = Db.getPeople
  }
  startWebServer defaultConfig personWebPart
  0
```

That's it! Now we have the HTTP GET Request up and running

{% img center border /images/sauve_rest_api/http_get.png %}

Since we are having no people in our in-memory dictionary, we are getting an empty result here. Let's add a new person using HTTP POST

## HTTP POST

HTTP POST uses the same workflow as HTTP GET. To implement this, we are going to use the following features in Suave.

* The ```choose``` function takes a list of WebParts, and chooses the first one that applies (i.e which returns ```Some```), or if none WebPart applies, then choose will  return ```None```

* The ```request``` function takes a function of type ```HttpRequest -> WebPart``` and returns the ```WebPart```. We will use this ```HttpRequest``` to get the POST content.

* The ```POST``` is a static in-built ```WebPart``` which matches the HTTP POST requests.

The first step in implementing POST request is updating the ```RestResource```.

```fsharp
type RestResource<'a> = {
  GetAll : unit -> 'a seq
  Create : 'a -> 'a        
}
```

Then add the following utility functions in ```Restful.fs``` to get the resource from the HttpRequest

```fsharp
let fromJson<'a> json =
  JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a    

let getResourceFromReq<'a> (req : HttpRequest) = 
  let getString rawForm = 
    System.Text.Encoding.UTF8.GetString(rawForm)
  req.rawForm |> getString |> fromJson<'a>
```

The ```rawForm``` field in the ```HttpRequest``` has the POST content as a byte array, we are just deserializing to a fsharp type.

The next step is updating the ```rest``` function to support POST

```fsharp
let rest resourceName resource =
  let resourcePath = "/" + resourceName
  let gellAll = warbler (fun _ -> resource.GetAll () |> JSON)
  path resourcePath >>= choose [
    GET >>= getAll
    POST >>= request (getResourceFromReq >> resource.Create >> JSON)
  ]
```
Thanks to the awesome function composition feature, we have combined all these tiny functions and implemented a new request and response.  

Then update ```Db.fs``` and ```App.fs``` respectively as follows

```fsharp
module Db = 
  let createPerson person =
    let id = peopleStorage.Values.Count + 1
    let newPerson = {
        Id = id
        Name = person.Name
        Age = person.Age
        Email = person.Email
    }
    peopleStorage.Add(id, newPerson)
    newPerson
```

```fsharp
let personWebPart = rest "people" {
  GetAll = Db.getPeople
  Create = Db.createPerson
}
```

The HTTP POST function in action

{% img center border /images/sauve_rest_api/http_post.png %}

## HTTP PUT

As we did for GET & POST we will be starting by updating the ```RestResource```

```fsharp
type RestResource<'a> = {
  GetAll : unit -> 'a seq
  Create : 'a -> 'a
  Update : 'a -> 'a option       
}
```
We have added a bit of error handling here. This ```Update``` function tries to update a resource and returns the updated resource if the resource exists. If it didn't it returns ```None```

To support HTTP PUT we will be using the following from suave library

* The ```BAD_REQUEST``` function takes a string and returns a WebPart representing HTTP status code 400 response with given string as it response.

* The ```PUT``` is a static in-built ```WebPart``` which matches the HTTP PUT requests.

```fsharp
let rest resourceName resource =
  let resourcePath = "/" + resourceName
  let badRequest = BAD_REQUEST "Resource not found"

  let gellAll = warbler (fun _ -> resource.GetAll () |> JSON)
  let handleResource requestError = function
    | Some r -> r |> JSON
    | _ -> requestError

  path resourcePath >>= 
    choose [
      GET >>= getAll
      POST >>= 
        request (getResourceFromReq >> resource.Create >> JSON)
      PUT >>= 
        request (getResourceFromReq >> resource.Update >> handleResource badRequest)
    ]
```
Then update ```Db.fs``` and ```App.fs``` as usual

```fsharp
module Db =   
  let updatePersonById personId personToBeUpdated =
    if peopleStorage.ContainsKey(personId) then
      let updatedPerson = {
        Id = personId
        Name = personToBeUpdated.Name
        Age = personToBeUpdated.Age
        Email = personToBeUpdated.Email
      }
      peopleStorage.[personId] <- updatedPerson            
      Some updatedPerson
    else 
      None
  let updatePerson personToBeUpdated =
    updatePersonById personToBeUpdated.Id personToBeUpdated
```
We have created one more function here ```updatePersonById``` which will be used later.

```fsharp
let personWebPart = rest "people" {
  GetAll = Db.getPeople
  Create = Db.createPerson
  Update = Db.updatePerson
}
```

**Updating an existing resource**

{% img center border /images/sauve_rest_api/http_put.png %}

**Updating a non-existing resource**

{% img center border /images/sauve_rest_api/http_put_error.png %}

## HTTP DELETE

Let's begin by updating the ```RestResource```

```fsharp
type RestResource<'a> = {
  GetAll : unit -> 'a seq
  Create : 'a -> 'a
  Update : 'a -> 'a option
  Delete : int -> unit    
}
```
HTTP DELETE is little different from the other implemented requests as we will be retrieving the id of the resource to be deleted from the URL.

For example, **DELETE /people/1** will delete a person with the id 1.  

To retrieve the id from the url, we will be using the ```pathScan``` function in Suave. This function similar to ```printf``` which takes a [PrintfFormat]() and a function which takes the output of the printfformat and returns the WebPart. You can get more details about it from the [suave-music-store book](http://theimowski.gitbooks.io/suave-music-store/content/url_parameters.html).

In addition to this, we will be using the following from suave

* The ```NO_CONTENT``` is a static ```WebPart``` represents the HTTP Response with the status code 204
* The ```DELETE``` matches the HTTP request of type DELETE

```fsharp
let rest resourceName resource =
  
  let resourcePath = "/" + resourceName
  let resourceIdPath = 
    new PrintfFormat<(int -> string),unit,string,string,int>(resourcePath + "/%d")

  let badRequest = BAD_REQUEST "Resource not found"

  let gellAll = warbler (fun _ -> resource.GetAll () |> JSON)
  let handleResource requestError = function
    | Some r -> r |> JSON
    | _ -> requestError
  let deleteResourceById id =
    resource.Delete id
    NO_CONTENT

  choose [
    path resourcePath >>= choose [
      GET >>= getAll
      POST >>= 
        request (getResourceFromReq >> resource.Create >> JSON)
      PUT >>= 
        request (getResourceFromReq >> resource.Update >> handleResource badRequest)
    ]
    DELETE >>= pathScan resourceIdPath deleteResourceById
  ]
```

One thing to notice here is we have wrapped the existing ```choose``` function with an another ```choose``` function. It may be little complex to understand but if you understand what each thing mean, it is easier. Here the inner ```choose``` function represents the handler functions for GET, POST & PUT requests having the url "/{resourceName}" and the ```DELETE``` webpart represents the HTTP DELETE handler for the url "/{resourceName}/{resourceId}".

The outer ```choose``` function chooses one of these based on the incoming request.

**Db.fs**

```fsharp
let deletePerson personId = 
  peopleStorage.Remove(personId) |> ignore
```

**App.fs**

```fsharp
let personWebPart = rest "people" {
  GetAll = Db.getPeople
  Create = Db.createPerson
  Update = Db.updatePerson
  Delete = Db.deletePerson
}
```
{% img center border /images/sauve_rest_api/http_delete.png %}

## HTTP GET & HTTP PUT by id

I hope by this time you know how to wire things up in Suave to create an API. Let's add features for getting and updating resource by id.

```fsharp
type RestResource<'a> = {
  GetAll : unit -> 'a seq
  Create : 'a -> 'a
  Update : 'a -> 'a option
  Delete : int -> unit 
  GetById : int -> 'a option
  UpdateById : int -> 'a -> 'a option   
}
```

```fsharp
let rest resourceName resource =
  
  // .. Existing code ...
  let getResourceById = 
    resource.GetById >> handleResource (NOT_FOUND "Resource not found")
  let updateResourceById id =
    request (getResourceFromReq >> (resource.UpdateById id) >> handleResource badRequest)

  choose [
    path resourcePath >>= choose [
      GET >>= getAll
      POST >>= 
        request (getResourceFromReq >> resource.Create >> JSON)
      PUT >>= 
        request (getResourceFromReq >> resource.Update >> handleResource badRequest)
    ]
    DELETE >>= pathScan resourceIdPath deleteResourceById
    GET >>= pathScan resourceIdPath getResourceById
    PUT >>= pathScan resourceIdPath updateResourceById
  ]
```

**Db.fs**

```fsharp
let getPerson id =
  if peopleStorage.ContainsKey(id) then
    Some peopleStorage.[id]
  else
    None
```

**App.fs**

```fsharp
let personWebPart = rest "people" {
  GetAll = Db.getPeople
  Create = Db.createPerson
  Update = Db.updatePerson
  Delete = Db.deletePerson
  GetById = Db.getPerson
  UpdateById = Db.updatePersonById
}
```

{% img center border /images/sauve_rest_api/http_get_id.png %}
{% img center border /images/sauve_rest_api/http_put_id.png %}

## HTTP HEAD

Http HEAD request checks whether the request with the given id is there or not. Its implementation is straight forward. 

```fsharp
type RestResource<'a> = {
  GetAll : unit -> 'a seq
  Create : 'a -> 'a
  Update : 'a -> 'a option
  Delete : int -> unit 
  GetById : int -> 'a option
  UpdateById : int -> 'a -> 'a option 
  IsExists : int -> bool
}

let rest resourceName resource =
  
  // .. Existing code ...
  let isResourceExists id =
    if resource.IsExists id then OK "" else NOT_FOUND ""

  choose [
    path resourcePath >>= choose [
      GET >>= getAll
      POST >>= 
        request (getResourceFromReq >> resource.Create >> JSON)
      PUT >>= 
        request (getResourceFromReq >> resource.Update >> handleResource badRequest)
    ]
    DELETE >>= pathScan resourceIdPath deleteResourceById
    GET >>= pathScan resourceIdPath getResourceById
    PUT >>= pathScan resourceIdPath updateResourceById
    HEAD >>= pathScan resourceIdPath isResourceExists
  ]
```
**Db.fs**

```fsharp
let isPersonExists = peopleStorage.ContainsKey
```

**App.fs**

```fsharp
let personWebPart = rest "people" {
  GetAll = Db.getPeople
  GetById = Db.getPerson
  Create = Db.createPerson
  Update = Db.updatePerson
  UpdateById = Db.updatePersonById
  Delete = Db.deletePerson
  IsExists = Db.isPersonExists
}
```
{% img center border /images/sauve_rest_api/head.png %}

That's all.. We have successfully implemented a REST API using Suave

## Extending with a new Resource

The beautiful aspect of this functional REST API design we can easily extend it to support other resources. 

Here is the rest API implementation of the ```albums``` resource in the [music-store](http://theimowski.gitbooks.io/suave-music-store/content/database.html) application. You can find the source code of MusicStoreDb [here](https://github.com/tamizhvendan/blog-samples/blob/master/SuaveRestApi/SuaveRestApi/MusicStoreDb.fs).

```fsharp
[<EntryPoint>]
let main argv =    

  let personWebPart = rest "people" {
    GetAll = Db.getPeople
    GetById = Db.getPerson
    Create = Db.createPerson
    Update = Db.updatePerson
    UpdateById = Db.updatePersonById
    Delete = Db.deletePerson
    IsExists = Db.isPersonExists
  }

  let albumWebPart = rest "albums" {
    GetAll = MusicStoreDb.getAlbums
    GetById = MusicStoreDb.getAlbumById
    Create = MusicStoreDb.createAlbum
    Update = MusicStoreDb.updateAlbum
    UpdateById = MusicStoreDb.updateAlbumById
    Delete = MusicStoreDb.deleteAlbum
    IsExists = MusicStoreDb.isAlbumExists
  }

  startWebServer defaultConfig (choose [personWebPart;albumWebPart])
  0 
```

{% img center border /images/sauve_rest_api/get_album_id.png %}

## Conclusion

In the amazing presentation on [Functional Programming Design Patterns](https://skillsmatter.com/skillscasts/6120-functional-programming-design-patterns-with-scott-wlaschin), Scott Wlaschin had this slide

{% img center border /images/sauve_rest_api/fp_design_patterns_slide.png %}

I wondered how this can be applied in real-time. By creating a rest API using suave I've understood this. 

> Just create functions and combine it to a build bigger system. 

What a nice way to develop a system! 

You can get the source code associated with this blog post in [my GitHub repository](https://github.com/tamizhvendan/blog-samples/tree/master/SuaveRestApi).