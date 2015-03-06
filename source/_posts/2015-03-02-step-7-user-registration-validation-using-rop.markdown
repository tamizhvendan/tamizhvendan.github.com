---
layout: post
title: "Step-7 User Registration Validation using ROP"
date: 2015-03-02 12:30:33 +0530
comments: true
categories: 
  - fsharp
  - ROP
---

> This is step 7 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In the [last blog post]({% post_url 2015-02-04-step-6-authentication-using-owin-middleware-and-asp-dot-net-identity %}) we have seen how to register a new user using Asp.Net Identity Management framework. Validating the incoming data before updating the backend datastore is a typical task in a web application. In this blog post we are going to see how to achieve it in a web application written in fsharp. As mentioned in the last blog post we will be adding validation logic for new user registration using [Railway oriented Programming](http://fsharpforfunandprofit.com/rop/) aka *ROP*.


### Cleanup

Before adding the validation let's do some cleanup and make it ready for adding validation. 

Create a new source file ```UserStorage``` in the **Identity** project and move the user manager creation logic from [MvcInfrastructure](https://github.com/tamizhvendan/fsharp-phonecat/blob/6/Web/MvcInfrastructure.fs#L15-L20) to here.

```fsharp
[<AutoOpen>]
module UserStorage =   
  
  let createUserManager () =
    let userStore = new UserStore<User>(new UserDbContext())
    let userManager = new UserManager<User>(userStore) 
    let userValidator = new UserValidator<User>(userManager)
    userValidator.AllowOnlyAlphanumericUserNames <- false   
    userManager.UserValidator <- userValidator
    userManager
```

Create a Request record type in ```Users``` that represents the request for creating a new user

```fsharp
type CreateUserRequest = {Name: string; Password: string; Email : string}
```

Then move the user creation logic from Authentication Controller's [Register action method](https://github.com/tamizhvendan/fsharp-phonecat/blob/6/Web/Controllers/AuthenticationController.fs#L62-L65) to ```UserStorage``` and update it to use ```CreateUserRequest```.

```fsharp
let createUser (userManager : UserManager<User>) createUserRequest =
    
  let user = new User(Name = createUserRequest.Name, 
                      UserName = createUserRequest.Email, 
                      Email = createUserRequest.Email)
  
  userManager.Create(user, createUserRequest.Password)
    
```

Finally update the ```Register``` action method in ```AuthenticationController``` to use the above function

```fsharp
[<HttpPost>]
[<ValidateAntiForgeryToken>]
member this.Register(registerViewModel : RegisterViewModel) : ActionResult =
  let createUserRequest : CreateUserRequest = {
    Name = registerViewModel.Name; 
    Email = registerViewModel.Email; 
    Password = registerViewModel.Password 
  }
  let userCreateResult = Users.createUser userManager createUserRequest
  if (userCreateResult.Succeeded) then
    signin userManager base.Request user      
    this.RedirectToAction("Index", "Home") :> ActionResult
  else
    userCreateResult.Errors
    |> Seq.iter(fun err -> this.ModelState.AddModelError("", err))
    this.View(registerViewModel) :> ActionResult
```

With this we are wrapping the cleanup work and all set for adding validation.

### Adding Validation

The first step in ROP is defining the two tracks. The track ```Success``` represents a successful operation (here its validation) and the other track ```Failure``` represents the failure while executing an operation. Usually this two tracks will be defined using a discriminated union.

Create a source file ```Rop``` in the **Identity** project and update it as below

```fsharp
module Rop = 
  type Error = {Property : string; Message : string}
  type Result<'a> = 
    | Success of 'a
    | Failure of Error
```  

The ```Error``` type provides the metadata associated with the error.

Let's start writing validation rules from validating Name. Create a source file ```NameValidation``` in **Identity** project and add the following code

```fsharp
module NameValidation = 
  let validateNameEmptiness createUserRequest =
    if (createUserRequest.Name <> null && 
          createUserRequest.Name <> String.Empty) then
      Success createUserRequest
    else
      Failure { Property = "Name"
                Message = "Name is required"}       

  let validateNameLength createUserRequest =
    if (createUserRequest.Name.Length <= 50) then
        Success createUserRequest
    else
      Failure { Property = "Name" 
                Message = "Name should not contain more than 50 characters"}
```

Hope these rules are self-explanatory. We are just validating the name for emptiness and length, and based on the result we are returning either the ```Success``` track or ```Failure``` track.

Next add validation rules for Password. Similar to Name Validation add a source file with the name ```PasswordValidation``` and update it as below

```fsharp
module PasswordValidation = 
  let validatePasswordEmptiness createUserRequest = 
    if (createUserRequest.Password <> null && 
          createUserRequest.Password <> String.Empty) then 
      Success createUserRequest
    else 
      Failure { Property = "Password"
                Message = "Password is required" }
  
  let validatePasswordLength createUserRequest = 
    if (createUserRequest.Password.Length <= 10) then 
      Success createUserRequest
    else 
      Failure { Property = "Password"
                Message = "Password should not contain more than 10 characters" }
  
  let validatePasswordStrength  = 
    let specialCharacters = [ "*"; "&"; "%"; "$" ]
    let hasSpecialCharacters (str : String) = 
      specialCharacters |> List.exists (fun c -> str.Contains(c))
    let errorMessage = 
      "Password should contain atleast one of the special characters " 
        + String.Join(",", specialCharacters)
    validate 
      (fun createUserRequest -> 
        hasSpecialCharacters createUserRequest.Password) 
      "Password" 
      errorMessage
```

In addition to the two validation rules that we seen in ```NameValidation``` here we have added one more validation to verify the presence of special characters in the ```Password```.

There is a pattern in all the valiadation rules that we have seen so far. 

{% img center /images/fsharp_phonecat/step_7/validation_pattern.png 450 450 %}

Let's extract this pattern out of these validation rules and reduce the lines of code!

Create a new source file ```Validation``` and add the write this pattern as a function

```fsharp
module Validation =  
  let validate isValid property message createUserRequest =
    if isValid createUserRequest then
      Success createUserRequest
    else
      Failure {Property = property; Message = message}      

```
The ```validate``` function represents the outerbox of the above diagram which takes four inputs

  * A function to validate the createUserRequest ```isValid``` which has the signature
    ```'a -> bool``` that represents the validation rule
  * A string to show which property is invalid in the error ```property```
  * A string representing the error message ```message```
  * The last is the incoming userCreateRequest ```createUserRequest```

*Note: Property and Message parameters are not shown in the diagram just to express the pattern clearly*

We also need a small utility function to check the emptiness of the string. So add it in ```Validation```

```fsharp
let isNonEmptyString str = str <> null && str <> String.Empty
```

With these functions in place we can refactor the validation rules that we written before.

```fsharp
module NameValidation = 
  let validateNameEmptiness =
    validate 
      (fun createUserRequest -> isNonEmptyString createUserRequest.Name) 
      "Name" 
      "Name is required"        

  let validateNameLength  =
    validate 
      (fun createUserRequest -> createUserRequest.Name.Length <= 50) 
      "Name" 
      "Name should not contain more than 50 characters"
```

```fsharp
module PasswordValidation = 
  let validatePasswordEmptiness =
    validate 
      (fun createUserRequest -> isNonEmptyString createUserRequest.Password) 
      "Password" 
      "Password is required"     
  
  let validatePasswordLength = 
    validate 
      (fun createUserRequest -> createUserRequest.Name.Length <= 10) 
      "Password" 
      "Password should not contain more than 10 characters"    
  
  let hasSpecialCharacters specialCharacters (str : String) = 
    specialCharacters |> List.exists (fun c -> str.Contains(c))
  
  let validatePasswordStrength  = 
    let specialCharacters = [ "*"; "&"; "%"; "$" ]
    let errorMessage = 
      "Password should contain atleast one of the special characters " 
        + String.Join(",", specialCharacters)
    validate 
      (fun createUserRequest -> 
        hasSpecialCharacters specialCharacters createUserRequest.Password) 
      "Password" 
      errorMessage
```
Much cleaner than the previous version isn't it ?

The final validation is Email validation. It is very similar to the other validations that we have seen so far. One extra validation is verifying the uniqueness of the email id being used to register.

Create a new source file ```EmailValidation``` and add the validations

```fsharp
module EmailValidation =   
  let isUniqueEmailAddr (userManager : UserManager<User>) emailAddr =
    let user = userManager.FindByName(emailAddr)
    user = null 
    
  let isValidEmailAddress emailAddr =
    let emailAddrRegex = 
      @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)" +
        "*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z"
    Regex.IsMatch(emailAddr, emailAddrRegex, RegexOptions.IgnoreCase)

  let validateEmailEmptiness =
    validate 
      (fun createUserRequest -> isNonEmptyString createUserRequest.Email) 
      "Email" 
      "Email is required"    

  let validateEmailUniqueness isUniqueEmailAddr createUserRequest =
    validate 
      (fun createUserRequest -> isUniqueEmailAddr createUserRequest.Email) 
      "Email" 
      "Email address already exists"    

  let validateEmailCorrectness =
    validate 
      (fun createUserRequest -> isValidEmailAddress createUserRequest.Email) 
      "Email" 
      "Email address is invalid"
```

### Putting all the validations together

So far we have seen individual properties validation rules. It's time put all these pieces togehter and validate the ```CreateUserRequest``` as a whole.

Create a source file ```UserValidation``` and update it as below

{% img /images/fsharp_phonecat/step_7/uservalidation_match.png %}

Wait.. Wait.. I know how do you feel here.. It's too much of code! Can we do it in a better way ? 



### Using Railway Oriented Programming (ROP)

Thanks to ROP, we can make the mighty code smaller and more readable. I am going ahead with an assumption that you are aware of ROP. In case if you are not aware of it then watch this [excellent presentation](http://vimeo.com/97344498) by [Scott Wlaschin](https://twitter.com/ScottWlaschin)

{% img center /images/fsharp_phonecat/step_7/bind_function.png 350 500 %}

The crux of ROP is the bind function which composes two functions where the output type of one function is not matching with that of input. If you want to know more this bind function, do visit this [awesome blog post](http://adit.io./posts/2013-04-17-functors,_applicatives,_and_monads_in_pictures.html) by [Aditya](https://twitter.com/_egonschiele). In this blog post he explains about the bind function under the title **Monads**

Let's start by adding this bind function in the module ```Rop``` that we have already created.

```fsharp
let bind f1 f2 x =
  match f1 str with
  | Success x -> f2 x
  | Failure err -> Failure err

let inline (>>=) f1 f2 = bind f1 f2
```

The bind function here calls the function ```f1``` with the given input ```x``` if it succeed then executes the second function ```f2``` with ```x```. In case if the execution of ```f1``` resulted in failure then it just passes without executing the second function. 

The infix operator ```>>=``` is a standard way of aliasing bind function. 

With the help of this bind function we can rewrite the ```validateCreateUserRequest``` function in a better way as below

```fsharp
module UserValidation = 
  let validateCreateUserRequest userManager  = 
      validateEmailEmptiness 
        >>= validateEmailCorrectness
        >>= validateEmailUniqueness (isUniqueEmailAddr userManager)
        >>= validateNameEmptiness
        >>= validateNameLength
        >>= validatePasswordEmptiness
        >>= validatePasswordLength
        >>= validatePasswordStrength
```

It's awesome! Isn't it ?? If you are still wondering what's happening here! Watch the Scott's presentation one more time.


### Adding validation before saving

Now we have the validation infrastructure in place. So, let's go ahead and add it before creating a new user. 