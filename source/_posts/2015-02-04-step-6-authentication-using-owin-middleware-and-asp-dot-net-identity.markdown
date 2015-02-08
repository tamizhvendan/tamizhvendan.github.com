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

Create a new F# class library project and name it as ```Identity```. and install the [Microsoft.AspNet.Identity.EntityFramework](https://www.nuget.org/packages/Microsoft.AspNet.Identity.EntityFramework/) nuget package which provides a pluggable data store for user identity management using Entity Framework.

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

The [AllowNullLiteral](https://msdn.microsoft.com/en-us/library/ee353608.aspx) attribute is one of the great feature of F# which explicitly make a type to allow null as one of its value. F# by default doesn't support null value for its types and the beauty is you will get a compiler error if you assign null to an identiter! (Pretty cool isn't it) No Null Exception. Then why we are allowing it explicitly here. Well, it has been added here for a reason and I will reveal it later in this blog post

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

Add reference to ```Idenity``` project in the ```Web``` project and install the following nuget packages

```text
Microsoft.Owin.Security.Cookies
Microsoft.Owin.Host.SystemWeb
Microsoft.AspNet.Identity.EntityFramework
```

Create a controller with the name ```AuthenticationController``` in the ```Web``` project and update it as below

```fsharp
[<CLIMutable>]
type RegisterViewModel = {
  Name : string
  Email : string
  Password : string
}

type AuthenticationController (userManager : UserManager<User>) = 
  inherit Controller()
  member this.Register() = this.View()

```
The ```AuthenticationController``` depends on ```UserManager``` to take care of managing users. [UserManager](https://msdn.microsoft.com/en-us/library/dn613290%28v=vs.108%29.aspx) is an abstraction defined in the **AspNet.Identity** assembly. It provides various APIs to work with the underlying user datastore. As the name indicates ```RegisterViewModel``` represents the view model for Registration View.

The action method ```Register``` simply renders the Register View. This view not created yet so lets add it. Create a cshtml file with the name *Register.cshtml* under the directory **Views/Authentication**. 

This is a strongly typed view of type ```RegisterViewModel```

```html
@model PhoneCat.Web.Controllers.RegisterViewModel

<h3>Register New User</h3>

@using (Html.BeginForm("Register", "Authentication", FormMethod.Post, new { @class = "form-horizontal" }))
{
  @Html.AntiForgeryToken()
  @Html.ValidationSummary()
  <div class="form-group">
    <label for="Email" class="col-sm-2 control-label">Name</label>
    <div class="col-sm-10">
      @Html.TextBoxFor(m => m.Name, new { @class = "form-control", placholder = "Name" })
    </div>
  </div>
  <div class="form-group">
    <label for="Email" class="col-sm-2 control-label">Email</label>
    <div class="col-sm-10">
      @Html.TextBoxFor(m => m.Email, new { @class = "form-control", placholder = "Email" })
    </div>
  </div>
  <div class="form-group">
    <label for="Password" class="col-sm-2 control-label">Password</label>
    <div class="col-sm-10">
      @Html.PasswordFor(m => m.Password, new { @class = "form-control", placholder = "Password" })
    </div>
  </div>
  <div class="form-group">
    <div class="col-sm-offset-2 col-sm-10">
      <button type="submit" class="btn btn-primary">Create</button>
    </div>
  </div>
}
```
{% img /images/fsharp_phonecat/step_6/new_user_registration.png %}

It's a typical razor view representing the registration screen. For the sake of simplicity I've ignored the retype password field. 

The next step is to handling this new user registration POST request.

```fsharp
[<HttpPost>]
[<ValidateAntiForgeryToken>]
member this.Register(registerViewModel : RegisterViewModel) : ActionResult =
  
  let user = new User(Name = registerViewModel.Name, 
                      UserName = registerViewModel.Email , 
                      Email = registerViewModel.Email)
  let userCreateResult = userManager.Create(user, registerViewModel.Password)
  if (userCreateResult.Succeeded) then          
    this.RedirectToAction("Index", "Home") :> ActionResult
  else
    userCreateResult.Errors
    |> Seq.iter(fun err -> this.ModelState.AddModelError("", err))
    this.View(registerViewModel) :> ActionResult 
```  

Add the above action method in the ```AuthenticationController``` to create new user. It creates a ```User``` object from the ```RegisterViewModel``` and use it to create a new user via ```UserManager```. We are using ```Email``` as the ```UserName``` in the above code snippet

If the user creation is successful, we are redirecting the user to the home page else we are showing the error messages to the user. After we implemented the login we will modify the above logic to sign in after successful registration.

The final pending task is creation of ```AuthenticationController``` instance. Open ```MvcInfrastructure``` that we have created in the step-2 and update it as below.

```fsharp
let private createUserManager () =
  let userManager = new UserManager<User>(new UserStore<User>(new UserDbContext()))
  let userValidator = new UserValidator<User>(userManager)
  userValidator.AllowOnlyAlphanumericUserNames <- false
  userManager.UserValidator <- userValidator
  userManager

type CompositionRoot(phones : seq<PhoneTypeProvider.Root>) =          
  inherit DefaultControllerFactory() with
    override this.GetControllerInstance(requestContext, controllerType) = 
      // ... Existing code ...
      else if controllerType = typeof<AuthenticationController> then
        let authenticationController = new AuthenticationController(createUserManager())
        authenticationController :> IController
      // ... Existing code ...

```
As we are using email as username we need to change the user validator in the ```UserManager``` to allow the alphanumeric characters in the username.

That's we have wired up everything to create a new user!

### Setting up User Login

As we are going to use owin [cookies based authentication](http://blogs.msdn.com/b/webdev/archive/2013/07/03/understanding-owin-forms-authentication-in-mvc-5.aspx) middleware, the first step to tell the application startup pipeline to use cookie authentication. 

Open ```Startup``` and update it to support authentication

```fsharp
open Owin
open Microsoft.Owin
open Microsoft.Owin.Security.Cookies
open Microsoft.AspNet.Identity

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
After setting up cookie authentication, like we did for new user registration, we are going to create a view to enable the user to login.

Add the ```Login``` action method in the ```AuthenticationController``` and also create ```LoginViewModel``` 

```fsharp
member this.Login() = this.View()
```
and create ```Login``` view in the **Authentication\Login** directory

```html
@model PhoneCat.Web.Controllers.LoginViewModel

<h3>Login</h3>
@using (Html.BeginForm("Login", "Authentication", FormMethod.Post, new { @class = "form-horizontal" }))
{
  @Html.AntiForgeryToken()
  @Html.ValidationSummary()
  <div class="form-group">
    <label for="inputEmail3" class="col-sm-2 control-label">Email</label>
    <div class="col-sm-10">
      @Html.TextBoxFor(m => m.Email, new { @class = "form-control", placholder = "Email" })
    </div>
  </div>
  <div class="form-group">
    <label for="inputPassword3" class="col-sm-2 control-label">Password</label>
    <div class="col-sm-10">
      @Html.PasswordFor(m => m.Password, new { @class = "form-control", placholder = "Password" })
    </div>
  </div>
  <div class="form-group">
    <div class="col-sm-offset-2 col-sm-10">
      <button type="submit" class="btn btn-primary">Log in</button>
    </div>
  </div>
}
```
{% img /images/fsharp_phonecat/step_6/login.png %}

The next step is the crux of this blog post. Challenging the 
