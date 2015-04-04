---
layout: post
title: "Step-10 Refactoring Composition Root"
date: 2015-04-02 17:07:23 +0530
comments: true
categories:
  - fsharp
---

> This is step 10 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

&nbsp;

In the fsharp-phonecat application that we are creating as part of this series, to [create the controller instances](https://github.com/tamizhvendan/fsharp-phonecat/blob/9/Web/MvcInfrastructure.fs#L14-L48), we are using [Pure DI](http://blog.ploeh.dk/2014/06/10/pure-di/). Though this [composition root](http://blog.ploeh.dk/2011/07/28/CompositionRoot/) solves the problem of [dependency injection](http://blog.ploeh.dk/2012/11/06/WhentouseaDIContainer/), the quality of the code is not up to the mark. 

In this blog post we are going to refactor this and make it shorter and more F# idiomatic.


### The current state of GetControllerInstance method

{% img border center /images/fsharp_phonecat/step_10/if_else_tangle.png %}

&nbsp;
&nbsp;

It's if full of **if..else if.. else if.. else** and the method is too long

As depicted in the above screenshot, the ```GetControllerInstance``` method does the following three things for all the controller types in the application

* Checks the incoming controller type
* Creates the requested controller instance
* Returns the created controller instance


{% img border center /images/fsharp_phonecat/step_10/home_controller_creation.png %}

This can be refactored to a function

{% img border center /images/fsharp_phonecat/step_10/home_controller_function.png %}

The function takes a type and returns an [Option type](http://fsharpforfunandprofit.com/posts/the-option-type/) of ```IController```

Let's do the same for an another controller creation which is little complex than this.

{% img border center /images/fsharp_phonecat/step_10/phone_controller_function.png %}

Here in this case to create an instance of ```PhoneController``` we need two additional parameters, Phones sequence and anonymous id of the session.

With these functions in place, the ```GetControllerInstance``` method would look like as below

{% img border center /images/fsharp_phonecat/step_10/match_with_some_and_none.png %}

Though the functions that have created has reduced the size of the method, it has introduced an another problem ([Pyramid Of Doom](http://raynos.github.io/presentation/shower/controlflow.htm?full#PyramidOfDoom)) as indicated by the arrow in the screenshot. 

We can get rid of this using a bind function as we did in [validation step using rop]({% post_url 2015-03-02-step-7-validation-and-error-handling-using-rop%}).

The downside of the [bind function](http://fsharpforfunandprofit.com/posts/computation-expressions-bind/) is it's not generic and we can't reuse the existing bind function that we have created for doing validation.

So, going with this approach would lead to adding more code which is not in alignment with the objective of this refactoring.

What can we do ??

&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;

The answer is **[Active Patterns](http://en.wikibooks.org/wiki/F_Sharp_Programming/Active_Patterns)**

> Active Patterns allow programmers to wrap arbitrary values in a union-like data structure for easy pattern matching. For example, its possible wrap objects with an active pattern, so that you can use objects in pattern matching as easily as any other union type -  &nbsp; *F# Programming WikiBook*

We are going to use [Partial Active Patterns](http://en.wikibooks.org/wiki/F_Sharp_Programming/Active_Patterns#Partial_Active_Patterns) to refactor the composition root.


### Creating Partial Active Patterns

Let's start by creating a partial active pattern for ```HomeController```

{% img border center /images/fsharp_phonecat/step_10/home_controller_active_pattern.png %}

As described in the screenshot, it's very similar to the ```createHomeController``` function and the only difference is the addition of the *Banana Clips* (yellow ellipses). The ```_``` symbol makes the active pattern partial.  

The partial active patterns always returns an ```Option``` type. 

Partial active patterns support defining pattern with multiple parameters. So we can change the ```createPhoneController``` function as below

{% img border center /images/fsharp_phonecat/step_10/phone_controller_active_pattern.png %}


Great! We are almost done with the refactoring. The pending step is leveraging these partial active patterns in the ```GetControllerInstance``` method.

It's where the **beauty of pattern matching** reveals!

{% img border center /images/fsharp_phonecat/step_10/home_controller_pattern_matching.png %}

What's happening here? 

* Based on the Active Pattern Name (```HomeController```), the control goes to the corresponding active pattern
* The value we are matching (```controllerType```) passed as argument.
* The value returned by the active pattern (instance of ```HomeController```) is assigned to the ```homeController``` literal in the match expression
* If none of the pattern has met the matching criteria, it executes the default match (raising the exception)

It does the same thing for the Active Patterns with multiple parameters

{% img border center /images/fsharp_phonecat/step_10/phone_controller_pattern_matching.png %}

The only thing to note here is, the value against which we are matching is always passed as a last argument.

That's it!!

After completing the creation of partial active patterns for all the other controller types in the application, the ```GetControllerInstance``` would be shorter like this

{% img border center /images/fsharp_phonecat/step_10/composition_root_final.png %}

&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;
&nbsp;

{% img border center /images/fsharp_phonecat/step_10/awesomeness.gif %}

### Summary

Moving from C# to F#, it's not just change in syntax! It's much more than that. The perfect analogy for this refactoring is the classic proverb *When in Rome, do as the Romans do*. You can find the complete source code of this step in the [phonecat github repository](https://github.com/tamizhvendan/fsharp-phonecat/tree/10)





