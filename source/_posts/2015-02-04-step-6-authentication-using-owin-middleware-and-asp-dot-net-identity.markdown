---
layout: post
title: "Step-6 Authentication using Owin Middleware and ASP.NET Identity"
date: 2015-02-04 17:24:22 +0530
comments: true
categories: 
  - fsharp
  - owin
  - Asp.Net-Identity
---

> This is step 6 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In the [last blog post]({% post_url 2015-01-18-step-5-advanced-search-dsl-using-fparsec %}) we have added an interactive feature to search phones and in this blog post we are going to do add authentication to our PhoneCat application using Owin Middleware and ASP.NET identity.

In the first half of this blog post we are going to see how we can implement a new user registration workflow 

{% img /images/fsharp_phonecat/step_6/registration_workflow.png %}

And in the second half, we are going to see how to do authentication using user login credentials

{% img /images/fsharp_phonecat/step_6/login_workflow.png %}


