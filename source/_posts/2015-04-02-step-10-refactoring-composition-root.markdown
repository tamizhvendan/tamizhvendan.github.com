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

In our fsharp-phonecat application to [create the controller instances](https://github.com/tamizhvendan/fsharp-phonecat/blob/9/Web/MvcInfrastructure.fs#L14-L48) we are using [Pure DI](http://blog.ploeh.dk/2014/06/10/pure-di/). Though this [compsoition root](http://blog.ploeh.dk/2011/07/28/CompositionRoot/) solves the problem of [dependency injection](http://blog.ploeh.dk/2012/11/06/WhentouseaDIContainer/), the quality of the code is not upto the mark

&nbsp;
&nbsp;

{% img border /images/fsharp_phonecat/step_10/if_else_tangle.png %}

&nbsp;
&nbsp;

Full of ```if else``` and it is 