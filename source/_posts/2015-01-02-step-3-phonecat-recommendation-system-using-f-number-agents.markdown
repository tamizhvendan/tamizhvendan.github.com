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

### Tracking user navigation

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

The ```StorageAgent``` is a typical F# Agent which waits for the incoming ```StorageAgentMessage``` and upon receiving it process it by storing them in the in-memory dictionary. 

The next step is wiring the ```PhoneViewTracker.observePhonesViewed``` function with the ```PhoneController.Show``` action method. We can call the function directly that will create a strongly coupled code. We can even directly post the message to the agent. But that also makes the code tightly coupled. 

What we are actually trying to implement here is a *Fire and Forget Approach*. Whenever the user visits a phone, we just want to notifiy somebody to keep track of it and move on. And its where [Reactive Extensions aka Rx](http://msdn.microsoft.com/en-in/data/gg577609.aspx) comes into the picture. 

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

But wait how we do we configure the subscription between this controller and the ```PhoneViewTracker``` ? Thanks to the CompositionRoot that we have [created in the step-2](https://github.com/tamizhvendan/fsharp-phonecat/blob/2/Web/Infrastructure.fs#L28-L32). As we have full control over the creation of controller its just a matter of two lines to achieve it.

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