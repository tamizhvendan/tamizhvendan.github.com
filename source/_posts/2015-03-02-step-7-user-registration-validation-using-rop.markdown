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

In the [last blog post]({% post_url 2015-02-04-step-6-authentication-using-owin-middleware-and-asp-dot-net-identity %}) we have seen how to register a new user using Asp.Net Identity Management framework. Validating the incoming data before updating the backend datastore is a typical task in a web application. In this blog post we are going to see how to achieve it in a web application written in fsharp. As mentioned in the last blog post we will be adding validation logic for new user registration.


