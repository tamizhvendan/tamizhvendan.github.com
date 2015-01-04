---
layout: post
title: "Step-3 PhoneCat Recommendation System using F# Agents, SignalR and Rx"
date: 2015-01-02 05:44:52 +0530
comments: true
categories: 
  - fsharp
  - functional-programming
  - ASP.NET MVC
  - Rx
  - SignalR
---

> This is step 3 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In the last two steps we have seen how to create a [web apis]({% post_url 2014-12-17-phonecat-backend-using-web-api-and-typeproviders %}) and [static razor views]({% post_url 2014-12-23-step-2-fsharp-phonecat-views-using-razor %}) in fsharp. In this blog post we are going to see one of my favorite feature in fsharp ["Message based approach to concurrency"](http://fsharpforfunandprofit.com/posts/concurrency-actor-model/).

### A Small Flashback
I've actually planned to put this blog post for the great fsharp community initiative [F# Advent Calender](https://sergeytihon.wordpress.com/2014/11/24/f-advent-calendar-in-english-2014/). Unfortunately it was not able to get through as I've [nominated myself](http://sergeytihon.wordpress.com/2014/11/24/f-advent-calendar-in-english-2014/#comment-4135) little late. I always believe there is an opportunity behind every adversity. I didn't get hung up and I knew this is one of the great topic to blog about. When I decided to blog about it, I needed a sample web application. So, I was creating that sample application and it suddenly strikes! *How about a blog series on web application development in fsharp?* I've immediately started working on it and hence this blog series.

### So what we gonna do in this step
In this blog post we are going to build a recommendation system which keeps track of what phones that the user is viewing, and based on his navigation history, we will be recommending a phone that he might be interested in

<p style="text-align:center"> <strong> User visits "Motorola XOOM™" </strong> </p>
{% img /images/fsharp_phonecat/step_3/Phone_1.png %}

<p style="text-align:center"> <strong> User visits "Motorola XOOM™ with Wi-Fi" </strong> </p>
{% img /images/fsharp_phonecat/step_3/Phone_2.png %}

Let us start the implementation by defining two high level tasks

1. Tracking user navigation
2. Recommending a phone

### 1. Tracking user navigation

{% img center /images/fsharp_phonecat/step_3/Phone_Visit_Workflow.png 600 500 %}

The first two components has been already implemented as part of [step-2]({% post_url 2014-12-23-step-2-fsharp-phonecat-views-using-razor %}). So we just need to wire up the other two components. Let's start from ```PhoneViewTracker```

Create a source file in the **Web** project and name it as ```PhoneViewTracker```. Add a function ```observePhonesViewed``` which will be invoked when you a user visits a phone.

```fsharp
module PhoneViewTracker =     
    
  let observePhonesViewed anonymousId phoneIdBeingVisited =
    StorageAgent.Post (SavePhoneVisit (anonymousId, phoneIdBeingVisited))
```

The ```anonymousId``` is a [http property](http://msdn.microsoft.com/en-us/library/system.web.httprequest.anonymousid%28v=vs.110%29.aspx) which represents a unique identifier for the given user session

Upon receiving the ```anonymousId``` and ```phoneIdBeingVisited``` we will be posting a message to the ```StorageAgent``` to save this visit. Both the agent and the message doesn't exist now, so lets create them

Create a source file in the **Domain** project and name it as ```UserNavigationHistory``` and add the following

```fsharp
namespace PhoneCat.Domain

open System.Reactive.Subjects
open System.Collections.Generic

type Agent<'T> = MailboxProcessor<'T>

module UserNavigationHistory =  

  type StorageAgentMessage =
    | SavePhoneVisit of string * string
  
  let private storageAgentFunc (agent : Agent<StorageAgentMessage>) =  
    
    let rec loop (dict : Dictionary<string, list<string>>) = async { 
      let! storageAgentMessage = agent.Receive()
      match storageAgentMessage with
      | SavePhoneVisit (anonymousId, phoneIdBeingVisited) -> 
          if dict.ContainsKey(anonymousId) then
            let phoneIdsVisited = phoneIdBeingVisited :: dict.[anonymousId]
            dict.[anonymousId] <- phoneIdsVisited
          else
            dict.Add(anonymousId, [phoneIdBeingVisited])
      return! loop dict      
    }

    loop (new Dictionary<string, list<string>>())

  let StorageAgent = Agent.Start(storageAgentFunc)

```


Storage Agent is an F# Agent which stores the user navigation history in an in-memory dictionary. It can be replaced by any key-value store like [redis](http://redis.io/) but for experimentation I've preferred to use F# Agents.

The ```StorageAgentMessage``` is a [dicriminated union](http://fsharpforfunandprofit.com/posts/discriminated-unions/) represents possible messages that the ```StorageAgent``` can process. Right now it has only message ```SavePhoneVisit``` which takes a tuple representing the ```anonymousId``` and the ```phoneIdBeingVisited```

The ```StorageAgent``` is a typical F# Agent which waits for the incoming ```StorageAgentMessage``` and upon receiving it stores the visit in the in-memory dictionary. 

The next step is wiring the ```PhoneViewTracker.observePhonesViewed``` function with the ```PhoneController.Show``` action method. We can call the function directly that will create a strongly coupled code. We can even directly post the message to the agent. But that also makes the code tightly coupled. 

What we are actually trying to implement here is a **User Phone Visit Stream**. Whenever the user visits a phone, we just want to notifiy somebody to keep track of it and move on. And its where [Reactive Extensions aka Rx](http://msdn.microsoft.com/en-in/data/gg577609.aspx) comes into the picture. If you are new to Reactive Programming or Rx, I strongly recommend you to go through [this excellent article](https://gist.github.com/staltz/868e7e9bc2a7b8c1f754) by [André Staltz](http://staltz.com/)

Install the [Rx Nuget Package](https://www.nuget.org/packages/Rx-Main/) in the *Web* project. With Rx in the kitty the next step is to make the User's phone visit as event and subscribe this event with the ```PhoneViewTracker```

The first step is to make the ```PhoneController``` as observable. Modify the already created ```PhoneController``` as below

```fsharp
type PhoneController(phones : seq<Phone>) =
  inherit Controller()
  
  let subject = new Subject<string>()
  
  interface IObservable<string> with
    member this.Subscribe observer = subject.Subscribe observer
  
  member this.Show (id : string) = 
    let phone = phones |> Seq.find (fun p -> p.Id = id) |> PhoneViewModel.ToPhoneViewModel
    subject.OnNext id 
    this.View(phone)

  override this.Dispose disposing =
    if disposing then 
      subject.OnCompleted()
      subject.Dispose()      
    base.Dispose disposing
```

One of the great feature of F# is its seamless interoperability with C# libraries. As you seen in the above code snippet we have just made the ```PhoneController``` into an observable by implementing the interface [IObservable](http://msdn.microsoft.com/en-us/library/dd990377%28v=vs.110%29.aspx). 

We have created a private [Rx Subject](http://msdn.microsoft.com/en-us/library/hh242970%28v=vs.103%29.aspx) and made it responsible for pushing the notification which contains the phone id that is being visited.

But wait how do we configure the subscription between this controller and the ```PhoneViewTracker``` ? Thanks to the CompositionRoot that we have [created in the step-2](https://github.com/tamizhvendan/fsharp-phonecat/blob/2/Web/Infrastructure.fs#L28-L32). As we have full control over the creation of controller its just a matter of two lines to achieve it.

```fsharp
type CompositionRoot(phones : seq<PhoneTypeProvider.Root>) =          
    inherit DefaultControllerFactory() with
      override this.GetControllerInstance(requestContext, controllerType) = 
        // ...
        else if controllerType = typeof<PhoneController> then          
          let phones' = phones |> Seq.map TypeProviders.ToCatalogPhone
          let observer = PhoneViewTracker.observePhonesViewed (requestContext.HttpContext.Request.AnonymousID)
          let phoneController = new PhoneController(phones')
          let subscription = phoneController.Subscribe observer                    
          phoneController :> IController
        // ...
```
We are leveraging the ASP.NET's [Anonymous Identification Module](http://msdn.microsoft.com/en-us/library/fsykd036.aspx) which help us in creating a unique anonymous id for every user session. We can retrieve it from the [HttpRequest](http://msdn.microsoft.com/en-us/library/system.web.httprequest.anonymousid.aspx) as mentioned in the above snippet

[Anonymous identification of user session](http://msdn.microsoft.com/en-in/library/91ka2e6a(v=vs.85).aspx) are not enabled by default, so add the following entry in the **Web.config** file

```xml
<configuration>
  <!-- Existing Code ignored for brevity ... -->
  <system.web>
    <!-- Existing Code ignored for brevity... -->
    <anonymousIdentification enabled="true" />
  </system.web>
</configuration>
```

With the help of the partial application of functions as we did it in the previous steps, we have created a partial function called ```observer``` which has the signature ```string -> unit```. Then we have subscribed to ```PhoneController``` using the ```Subscribe``` method and with this we are done with saving an user visit. 

### 2. Recommending a phone

**Workflow**

{% img center /images/fsharp_phonecat/step_3/Phone_Recommendation_Workflow.png 600 500 %}

1. User initiates the recommendation request using SignalR
2. Upon receiving it, Recommendation SignalR hub sends a recommendation request message to Storage Agent with the user Anonymous Id and SignlaR connection Id of the given user
3. Storage Agent then fetches the phone visit history of the given user based on the incoming anonymous id and pass it to the Recommendation Agent along with the SignalR connection id.
4. Recommendation Agent responds to this by computing the recommendation and publish the result (Either recommended phone id or none) in the Recommendation observable
5. Recommendation hub receives this recommendation result, send the response back to the corresponding SignalR client.

The beauty of this entire workflow is all are message driven and asynchronous by default.  

Let's start from Recommendation SignalR hub. The first step is installing SingalR from [the nuget](https://www.nuget.org/packages/Microsoft.AspNet.SignalR/2.1.2).

After installing create a source file in the **Web** project and name it as ```Startup``` then add the following code as per the SignalR convention.

```fsharp
namespace PhoneCat.Web

open Owin

type Startup() = 
  member x.Configuration(app : IAppBuilder) = 
    app.MapSignalR() |> ignore
    ()
```

Then add an app setting in the *Web.config* file and configure the SignalR to use this ```Startup``` class

```xml
<configuration>
  <appSettings>
    <!-- Other keys.. -->
    <add key="owin:AppStartup" value="PhoneCat.Web.Startup" />
  </appSettings>
  <!-- other configuration items.. -->
</configuration>
```

With SignalR added to the system, the next step is to create ```RecommendationHub```. Add a source file in **Web** project and name it as ```Hubs```.

Then create a ```RecommendationHub``` class with a public method ```GetRecommendation``` which will be invoked by the SignalR client to initiate recommendation process.

```fsharp
type RecommendationHub() =
    inherit Hub ()
    member this.GetRecommendation () =             
      let encodedAnonymousId = this.Context.Request.Cookies.[".ASPXANONYMOUS"].Value
      let anonymousId = decode encodedAnonymousId
      let connectionId = this.Context.ConnectionId
      StorageAgent.Post (GetRecommendation(anonymousId, connectionId))
      "Recommendation initiated"
```

Then anonymous id of the user session is actually persisted in the [request cookies by Asp.Net](http://msdn.microsoft.com/en-in/library/91ka2e6a%28v=vs.85%29.aspx) in an encoded format. In the ```GetRecommendation``` method we will be retrieving this encoded anonymous id from the cookie and decode it. Then we need to get the SignalR connection id which available in the base class ```Hub```. After getting both the anonymous id and the connection id, send a ```GetRecommendation``` message to the ```StorageAgent``` with these information. Finally send a response to the SignalR client as "Recommendation initiated".

The ```decode``` function is not added yet so let's add them. Thanks to [this stackoverflow answer](http://stackoverflow.com/a/2481110/159850) we just need to convert the code from C# to F#

```fsharp
let private decode encodedAnonymousId =
    let decodeMethod = 
      typeof<AnonymousIdentificationModule>
        .GetMethod("GetDecodedValue", BindingFlags.Static ||| BindingFlags.NonPublic)
    let anonymousIdData = decodeMethod.Invoke(null, [| encodedAnonymousId |]);
    let field = 
      anonymousIdData.GetType()
        .GetField("AnonymousId", BindingFlags.Instance ||| BindingFlags.NonPublic);
    field.GetValue(anonymousIdData) :?> string
```

We have used a special F# operator here ```:?>``` which is a dynamic down cast operator which casts a base class to a sub-class of it. You can read [this msdn documentation](http://msdn.microsoft.com/en-us/library/dd233220.aspx) to know more about F# casting and conversions.

The ```GetRecommendation``` message is not added yet, so let's add them too. Modify ```StorageAgentMessage``` created before as below

```fsharp
  type StorageAgentMessage =
    | SavePhoneVisit of string * string
    | GetRecommendation of string * string
```

The final step of this pipeline is to Update the ```StorageAgent``` to handle this ```GetRecommendation``` message. Modify the ```storageAgentFunc``` in the ```UserNavigationHistory``` as below

```fsharp
 let private storageAgentFunc (agent : Agent<StorageAgentMessage>) =  
    let rec loop (dict : Dictionary<string, list<string>>) = async { 
      let! storageAgentMessage = agent.Receive()
      match storageAgentMessage with
      | SavePhoneVisit (anonymousId, phoneIdBeingVisited) -> 
          // .. existing code ignored for brevity ..
      | GetRecommendation (anonymousId, connectionId) ->
          if dict.ContainsKey(anonymousId) then
            let phoneIdsVisited = dict.[anonymousId]
            RecommendationAgent.Post (connectionId,phoneIdsVisited)
      return! loop dict
```

Handling of the ```GetRecommendation``` message is very straight forward. Just get the phone ids being visited by the given anonymous id from the in memory dictionary and send a message consists of connection id and this phone ids visited to the ```RecommendationAgent``` which we will be creating next.

Create a source file with the name ```Recommendations``` in the **Domain** project and add the ```RecommendationAgent``` below

```fsharp
[<AutoOpen>]
module Recommendation =
  let private recommendationAgentFunc (inbox : Agent<string*List<string>>) =
    let rec messageLoop () = async {
      let! connectionId, visitedPhoneIds = inbox.Receive()     
      if (Seq.length visitedPhoneIds) >= 2 then
        suggestRecommendation connectionId (visitedPhoneIds |> Seq.take 2 |> Seq.toList)
      return! messageLoop()
    }
    messageLoop ()

  let RecommendationAgent = Agent.Start recommendationAgentFunc
``` 

In order to keep this blog post simple I've used my own 'Hello World' kind of algorithm which picks the latest two phone ids being visited and suggests recommendation based on it. In a real world you would be replacing this toddler algorithm with more sophisticated algorithms like [Association Rule Learning](http://en.wikipedia.org/wiki/Association_rule_learning). I am planning to implement this algorithm at later stages of this blog series.

Then the next step is to implement the ```suggestRecommendation``` function which picks a hardcoded recommendation and publish the result using Rx. To do this add the [Rx Nuget Package](https://www.nuget.org/packages/Rx-Main/) in the **Domain** project and add the  ```suggestRecommendation``` function in the ```Recommendation``` module

```fsharp
let RecommendationPipe = new Subject<string*string option>()

  let private suggestRecommendation connectionId visitedPhoneIds = 
    match visitedPhoneIds with
    | ["motorola-xoom-with-wi-fi"; "motorola-xoom"] -> RecommendationPipe.OnNext (connectionId, Some "motorola-atrix-4g")
    | ["dell-streak-7"; "dell-venue"] -> RecommendationPipe.OnNext (connectionId, Some "nexus-s")
    | _ -> RecommendationPipe.OnNext (connectionId, None)
```

The implementation is a very straight forward pattern matching. If last visited two items are ("motorola-xoom-with-wi-fi", "motorola-xoom"), send the recommendation as "motorola-atrix-4g", else if they are ("dell-streak-7","dell-venue") then recommend "nexus-s". If none of the condition matches then send none. Thanks to the [option type](http://fsharpforfunandprofit.com/posts/the-option-type/) which expresses this result in type safe manner.

With all these infrastructure in place, all we need to do is to just subscribe to this ```RecommendationPipe``` and send the suggestion to the user via SignalR

Let's add a observer for this pipe in the **Web** project. Open ```Hubs``` module in the **Web** project and add the following code

```fsharp

let private getUrl (phoneId : string) httpContext =
    let routeValueDictionary = new RouteValueDictionary()
    routeValueDictionary.Add("controller", "Phone")
    routeValueDictionary.Add("action", "Show")
    routeValueDictionary.Add("id", phoneId)
    let requestContext = new RequestContext(new HttpContextWrapper(httpContext), new RouteData());
    let virtualPathData = RouteTable.Routes.GetVirtualPath(requestContext, routeValueDictionary);
    virtualPathData.VirtualPath

let notifyRecommendation httpContext phones (connectionId, recommendedPhoneId) =
    let phones' = phones |> Seq.map TypeProviders.ToPhone
    match recommendedPhoneId with 
    | Some phoneId ->
      let recommendedPhone = Phones.getPhoneById phones' phoneId
      let phoneUrl = getUrl phoneId httpContext
      let hubContext = GlobalHost.ConnectionManager.GetHubContext<RecommendationHub>()
      hubContext.Clients.Client(connectionId)?showRecommendation(recommendedPhone, phoneUrl) 
    | None -> ()
```

The ```notifyRecommendation``` function checks whether the incoming recommendedPhoneId has value or not. If it has value, it just picks the ```Phone``` record corresponding to the given phoneId and get the url for the recommended phone. With all these data in place, we just need to send the response to the using via SignalR.

You would have a noticed a weird ```?``` symbol which is actually part of the [ImpromptuInterface.FSharp](https://www.nuget.org/packages/ImpromptuInterface.FSharp/). This library adds provisions to use [C# dynamic types](http://msdn.microsoft.com/en-IN/library/dd264736.aspx) in F#

They are two missing pieces. One is ```Phones.getPhoneById``` which we are not having. Let's add them. Open ```Phones``` module in **Domain** project and add it as mentioned below

```fsharp
let getPhoneById (phones : seq<Phone>) phoneId =
    phones |> Seq.find (fun p -> p.Id = phoneId)
```

The final step is wiring the ``RecommendationPipe`` with the ```notifyRecommendation```. Open ```Global.asax.fs``` and update it as below

```fsharp
type Global() = 
  // .. existing code ignore for brevity ..
 member x.Application_Start() =
    let phones = GitHubRepository.getPhones()
    // .. existing code ignore for brevity ..
    let notificationObserver = Hubs.notifyRecommendation HttpContext.Current phones
    Recommendation.RecommendationPipe.Subscribe notificationObserver |> ignore
    ()
```

Partial application of function is a very handy thing which actually replaces its counterpart Dependency Injection in the OOP. We just provided the first two arguments of ```notifyRecommendation``` and created a new function with the signature ```string * string option -> unit``` which is the expected observer signature for the ```RecommendationPipe```.

#### The Front End UI
The front end for this is a typical SignalR-Javascript client code which you can find it the [github repository](https://github.com/tamizhvendan/fsharp-phonecat/blob/3/Web/Scripts/recommendation.js). I've intentionally leaving the front-end part of this application as it would be extends the scope of the blog post. Moreover if you go through the source code in the github repository you can easily understand


### Summary
F# is just so awesome with so much expressive functional programming features. Rx, Agents and SignalR add more powers to it and enable you to create a scalable functional reactive architecture. I'd like give credits two incredible resources on this subject Mark Seemann's Pluralsight course on [Functional Architecture with F#](http://www.pluralsight.com/courses/functional-architecture-fsharp) and Kevin Ashton's excellent blog post on [Full Stack F#](http://namelessinteractive.com/blog/Full_Stack_FSharp_%E2%80%93_The_Long_Version_(Part_1)) which helped me a lot in coming out with this blog post.

Last but not the least, Thanks to [Sergey Tihon](https://sergeytihon.wordpress.com) for the words of encouragement to write the blog post on this topic.

You can find the source code of this step in [the github repository](https://github.com/tamizhvendan/fsharp-phonecat/tree/3). 