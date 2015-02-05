---
layout: post
title: "Step-6 Authentication using Owin authentication Middleware and ASP.NET Identity"
date: 2015-02-04 17:24:22 +0530
comments: true
categories: 
  - fsharp
  - owin
  - Asp.Net-Identity
---

> This is step 6 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In the [last blog post]({% post_url 2015-01-18-step-5-advanced-search-dsl-using-fparsec %}) we have added an interactive feature to search phones and in this blog post we are going to do add authentication to our PhoneCat application using Owin Authentication Middleware and ASP.NET identity.

In the first half of this blog post we are going to see how can we implement a new user registration workflow 

{% img /images/fsharp_phonecat/step_6/registration_workflow.png %}

And in the second half, we are going to see how to do authentication using the login credentials

{% img /images/fsharp_phonecat/step_6/login_workflow.png %}

### Owin Authentication Middleware in brief

In the [Owin Specification](https://msdn.microsoft.com/en-us/magazine/dn451439.aspx), a middleware access the request before it reaches the underlying web framework. You can view the authentication middleware as gate keeper who checks the incoming request, if it is authenticated it let the request to go through the other middlewares in the pipeline else it bypasses the pipeline and take a different route (usually redirecting to the login page)

In the blog post we are going to use cookie based authentication middleware which is similar to the [Asp.Net Forms Authentication](https://msdn.microsoft.com/en-us/library/7t6b43z4%28v=vs.140%29.aspx) and it also supports [Claims](https://msdn.microsoft.com/en-us/library/system.security.claims.claim(v=vs.110).aspx).

Another appreciable feature in Owin it is upto us on how to validate the incoming user credentials. We can plug it to any external authentication providers like Google, Facebook, etc., or using Active Directory or Asp.Net Identity. Here we are going to validate the user credentials using [Asp.Net Identity framework](http://odetocode.com/blogs/scott/archive/2013/11/25/asp-net-core-identity.aspx) which provides the required interfaces for handling the authentication and [Entity Framework Identity](http://odetocode.com/blogs/scott/archive/2014/01/03/asp-net-identity-with-the-entity-framework.aspx) which provides the concrete implementations for the Identity interfaces.

### Setting up the infrastructure

Create a new F# class library project and name it as ```Identity```. and install the [Microsoft.AspNet.Identity.EntityFramework](https://www.nuget.org/packages/Microsoft.AspNet.Identity.EntityFramework/) nuget package which provides a pluggable data store for user identity data using Entity Framework.

Then add a source file in the ```Identity``` project with the name ```User``` and update it as below

```fsharp
open Microsoft.AspNet.Identity.EntityFramework

[<AllowNullLiteral>]
type User() = 
  inherit IdentityUser()
  member val Name = "" with get, set

type UserDbContext() =
  inherit IdentityDbContext<User>("IdentityConnection")
```

The ```IdentityUser``` represents default implementation of IUser interface which provides the basic properties for a user. We can extend it by adding our custom properties. Here we have added a custom property called ```Name``` which represents the name of the user.

The ```IdentityDbContext``` represents the data store abstraction of Identity entities like Users, Roles, Claims and Logins. The ```UserDbContext``` is just a subclass of ```IdentityDbContext``` which tells Entity Framework to use the connection string name ```IdentityConnection```

In the ```Web``` project add the reference to the ```Identity``` project and install the following nuget packages

```text
Microsoft.Owin.Security.Cookies
Microsoft.Owin.Host.SystemWeb
Microsoft.AspNet.Identity.EntityFramework
```

After installation modify the ```Startup``` class to use Cookie based authentication.

```fsharp
type Startup() = 

  let configureAuthentication (app : IAppBuilder) =
    let cookieAuthenticationOptions = new CookieAuthenticationOptions()
    cookieAuthenticationOptions.AuthenticationType <- DefaultAuthenticationTypes.ApplicationCookie 
    cookieAuthenticationOptions.LoginPath <- new PathString("/authentication/login")
    app.UseCookieAuthentication(cookieAuthenticationOptions) |> ignore
    
  
  member x.Configuration(app : IAppBuilder) = 
    app.MapSignalR() |> ignore
    configureAuthentication app
    ()
```
The ```AuthenticationType``` is an identifier to distinguish between different authentication middlewares and the ```LoginPath``` is the url to the Login Page which will be used to redirect the unauthorized requests.

### Setting up User Registration

With all the needed infrastructure in place its time to do the actual work and lets start with adding a provision for registering new user.







