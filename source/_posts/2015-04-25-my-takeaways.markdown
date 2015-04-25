---
layout: post
title: "My Takeaways"
date: 2015-04-25 15:09:02 +0530
comments: true
categories: 
  - fsharp
  - functional-programming
---

> This is the concluding part of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

I would like to begin this blog post by thanking [John Sonmez](https://twitter.com/jsonmez) for his inspirational video on [the importance of finishing what you started](http://simpleprogrammer.com/2014/01/09/importance-finishing-started/). I haven't written any blog series [like this]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %}) before, so during this course, I was about to give up but the statement 

  <p style="text-align:center"> <strong> Finish what you started </strong> </p>

kept me going. Thank you John, I have learned a great lesson in my life from you.




Back to the business, In this blog post I am going to share my key takeaways on developing a complete web application in fsharp using ASP.NET MVC


## Enterprise application development in fsharp

As a learner of F#, I have spent lot of time in listening to [the great talks](https://vimeo.com/channels/c4fsharp) by the vibrant [fsharp community](http://c4fsharp.net/). Though those talks are great, I was intrigued, how these individual pieces would work together in creating a complete enterprise application. Is it really possible? 

Developing this complete [fsharp-phonecat](https://github.com/tamizhvendan/fsharp-phonecat) application answered my questions and I am sure it answered yours too. It may appear hard because of less sources and functional paradigm, but once you figured it out, it would be a cakewalk.

The bottom line is **we can create a complete enterprise application in fsharp**. As a matter of fact there is a book written on this subject, [F# Deep Dives](http://www.amazon.com/gp/product/1617291323/ref=as_li_tl?ie=UTF8&camp=1789&creative=9325&creativeASIN=1617291323&linkCode=as2&tag=bor0b-20&linkId=SYG2CSFARCGD5KUL)

If you not convinced yet, read [these testimonials](http://fsharp.org/testimonials/) from the commercial users of F#. 


## Leveraging the existing .NET ecosystem

In my perspective F# is a *pragmatic functional programming language*. In addition to lot of elegant features in the language, it works seamlessly with the existing .NET libraries. This one characteristic I would say is a blessing. You can say, 'This feature is really cool', but at the end of the day it should help you to get the job done. F# is not only cool, It can help you too!

In the sample application we have leveraged the [Web Api 2]({% post_url 2014-12-17-phonecat-backend-using-web-api-and-typeproviders %}), [Razor Views]({% post_url 2014-12-23-step-2-fsharp-phonecat-views-using-razor %}), [Asp.Net Identity]({% post_url 2015-02-04-step-6-authentication-using-owin-middleware-and-asp-dot-net-identity %}) and the Asp.Net framework itself. So, you can do whatever you are doing in C# in F# too (with less lines of code {% emoji smile %} )

One downside of this is the integration points may not look like idiomatic fsharp code as these frameworks not meant for fsharp. 

## Using F# in your existing C# Codebase

As F# is a [CLI Language](http://en.wikipedia.org/wiki/List_of_CLI_languages#CLI_languages) it can be added to your existing C# solution. All you need to do just create a new F# project in the solution and this project can work seamlessly with the other projects in the solution. 

