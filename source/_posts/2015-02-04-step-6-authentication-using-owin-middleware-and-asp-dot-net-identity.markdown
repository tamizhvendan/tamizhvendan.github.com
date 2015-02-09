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

The [AllowNullLiteral](https://msdn.microsoft.com/en-us/library/ee353608.aspx) attribute is one of the feature in F# which explicitly make a type to allow null as one of its value. F# by default doesn't support null value for its types and the beauty is you will get a compiler error if you assign null to an identiter! Pretty cool isn't it? No Null Exception, [No Billion Doller Mistake](http://www.infoq.com/presentations/Null-References-The-Billion-Dollar-Mistake-Tony-Hoare)! Then why we are allowing it explicitly here. Well, it has been added here for a reason and I will reveal it later in this blog post

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
      
  @Html.TextBoxFor(m => m.Name)
    
  @Html.TextBoxFor(m => m.Email)
   
  @Html.PasswordFor(m => m.Password, new { @class = "form-control", placholder = "Password" })
      
  <button type="submit" class="btn btn-primary">Create</button>  
}
```
*Note: I've intentionally ignored the bootstrap css form styles here to keep the code snippet less noisy*

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
@using (Html.BeginForm("Login", "Authentication", FormMethod.Post))
{
  @Html.AntiForgeryToken()
  @Html.ValidationSummary()
  
      @Html.TextBoxFor(m => m.Email)
    
      @Html.PasswordFor(m => m.Password)
    
      <button type="submit">Log in</button>   
}
```
{% img /images/fsharp_phonecat/step_6/login.png %}

The next step is the crux of this blog post. Challenging the incoming user login credentials against its corresponding registered one.

We will be using ```UserManager```'s following methods to achieve it.

* [Find](https://msdn.microsoft.com/en-us/library/dn497475(v=vs.108).aspx) - Returns a user with the specified username and password or null if there is no match (C# needs [Option](http://fsharpforfunandprofit.com/posts/the-option-type/) type badly!). In the beginning of the blog post I've mentioned you that I will talk about why we are using ```AllowNullLiteral``` attribute for the ```User``` class. As you see here ```Find``` method returns ```null``` if the user is not available! So, As per this definition ```null``` is valid value for ```User``` class.

* [CreateIdentity](https://msdn.microsoft.com/en-us/library/dn497467(v=vs.108).aspx) - Creates the Claim Identity representing the user. We need this claim identity to signin and also to pass around the claim details. 

Both of the above methods are having their async counterparts. But for the sake of simplicty I'm ignoring it. May be it can be a exercise for you to use figure it out!

Let's add some utility function to handle finding the user and signing in

```fsharp
let signin (userManager : UserManager<User>) (request : HttpRequestBase) user  =
  let identity = userManager.CreateIdentity(user, DefaultAuthenticationTypes.ApplicationCookie)
  let authManager = request.GetOwinContext().Authentication
  identity.AddClaim(new Claim(ClaimTypes.GivenName, user.Name))
  authManager.SignIn(identity)

let tryFindUser (userManager : UserManager<User>) email password  =
  let user = userManager.Find(email, password)
  if user <> null then Some user else None
```
Since we are using Email as the UserName here in this example, we need to have a specific claim to pass around the Name of the user. That's what we are doing in the ```signin``` method. Later we will be using this claim to display the user name in the header of the page.

Great! Now its time to handle handle Login POST request. 

```fsharp
[<HttpPost>]
[<ValidateAntiForgeryToken>]
member this.Login(loginViewModel : LoginViewModel) : ActionResult =
  match tryFindUser userManager loginViewModel.Email loginViewModel.Password  with
  | None ->
    this.ModelState.AddModelError("", "Invalid Email or Password")
    this.View(loginViewModel) :> ActionResult
  | Some user ->
    signin userManager base.Request user
    this.RedirectToAction("Index", "Home") :> ActionResult    
```

The ```Login``` action method just tries to find the user using given credentials. If the user is available, signin to the application using his credentials and redirect to the home page else show login error to the user.

We can add this same signin behavior after successful user registration too.

```fsharp
[<HttpPost>]
[<ValidateAntiForgeryToken>]
member this.Register(registerViewModel : RegisterViewModel) : ActionResult =  
  // ... Existing Code ...
  if (userCreateResult.Succeeded) then
    signin userManager base.Request user      
    this.RedirectToAction("Index", "Home") :> ActionResult
  else
    // ... Existing Code ...
``` 

Since ```UserManager``` is an ```IDisposable```. It's good practices to dispose it after using. So, Override ```Dispose``` method in ```AuthenticationController``` and dispose it. 

```fsharp
override this.Dispose(disposing) =
  if disposing then
    userManager.Dispose()
  base.Dispose(disposing)
```

The final pending work is displaying the user name in the header after successful login. Open **Layout.cshtml** add the following lines

```html
<!-- ... Existing code ... -->
<ul class="nav navbar-nav navbar-right top-nav">

  @if (Request.IsAuthenticated)
  {
    var identity = User.Identity as ClaimsIdentity;
    var name = identity.FindFirst(ClaimTypes.GivenName).Value;
    <li class="dropdown">
      <a href="#" class="dropdown-toggle" data-toggle="dropdown" aria-expanded="true">
        <i class="fa fa-user"></i>@name
      </a>
      <ul class="dropdown-menu">
        <li>
          <a href="@Url.Action("Logout","Authentication")">
            <i class="fa fa-fw fa-power-off"></i> Log Out
          </a>
        </li>
      </ul>
    </li>
  }
  else
  {
    <li class="dropdown">
      <a href="#" class="dropdown-toggle" data-toggle="dropdown" aria-expanded="true">
        <i class="fa fa-user"></i> Guest
      </a>
      <ul class="dropdown-menu">
        <li>
          <a href="@Url.Action("Login","Authentication")">
            <i class="fa fa-fw fa-bank"></i> Log In
          </a>
        </li>
        <li>
          <a href="@Url.Action("Register","Authentication")">
            <i class="fa fa-fw fa-laptop"></i>Register
          </a>
        </li>
      </ul>
    </li>
  }
</ul>
<!-- ... Existing code ... -->
```

### Adding Logout

Adding logout is very simple and straight forward. All we need to do is just invoke the Owin Authentication Manager's ```SignOut`` method. Create an action method in ```AuthenticationController``` to handle it

```fsharp
member this.Logout() =
  let authManager = base.Request.GetOwinContext().Authentication
  authManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie)
  this.RedirectToAction("Index", "Home")
```

### Summary

The interoperability offered by F# to integrate with the existing C# libraries is very seamless and I hope you have got it too! You can find the source code in the [github](https://github.com/tamizhvendan/fsharp-phonecat/tree/6) as usual. In the next blog post We will see how to add validations in the User Registration. Stay tuned!

