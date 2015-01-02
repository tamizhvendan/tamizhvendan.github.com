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

#### So what we gonna do in this step

