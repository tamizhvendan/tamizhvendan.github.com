---
layout: post
title: "Some tips for Using C# Libraries in F#"
date: 2015-04-30 11:19:53 +0530
comments: true
categories: 
  - fsharp  
---

In my [last blog post]({% post_url 2015-04-25-my-takeaways %}) on what I've learned while developing a complete web application in fsharp, I've mentioned that we can leverage all the libraries that were written for csharp in fsharp. 

Fsharp is an hybrid programming language which supports both functional programming and object oriented programming. Though you can write an application completely in functional way, when you need to integrate with other .NET languages, you may need to use some OO features.

As both the languages has its own design and principles (immutable types, not allowing null, etc.,), the integration of these is not straightforward in certain scenarios. In this blog post you are going to learn how to handle these tricky scenarios. 


## AllowNullLiteral Attribute

When you [create a class in F#](http://fsharpforfunandprofit.com/posts/classes/), by default we can't explicitly assign a null value to a instance of the class unlike C#. You will get a compiler error when you do it. It's a great feature and it helps in getting rid of Null Reference exceptions. 

{% img center /images/fs_cs_interop/NullError.png %}

But in certain cases, we may need to assign a null value to a class. For example, let us take the [Find method](https://msdn.microsoft.com/en-us/library/dn497475(v=vs.108).aspx) in the Asp.Net identity framework. It returns a user with the specified username and password or null if there is a no match. 

When you try to use this in a fsharp code, you will be getting a compiler error

{% img center /images/fs_cs_interop/UserNullError.png %}

It's where [AllowNullLiteral attribute](https://msdn.microsoft.com/en-us/library/ee353608.aspx?f=255&MSPPError=-2147217396) comes into picture. If you want to make a class type to allow null literal as one of its value, you need to decorate that class with the AllowNullLiteral attribute.

{% img center /images/fs_cs_interop/AllowNullAttr.png %}

## CLIMutable Attribute

When a fsharp Record type is compiled, it has been translated to a Common Language Infrastructure (CLI) representation of a typical [value object declation](http://blog.pluralsight.com/domain-driven-design-in-csharp-implementing-immutable-value-objects) in C#.

 {% img center border /images/fs_cs_interop/Record_Type_Compilation.png %}

The important thing to notice here is there is no default constructor! 

Certain libraries and frameworks requires a class to contain default constructor. For example, the [model binding](http://www.asp.net/web-api/overview/formats-and-model-binding/parameter-binding-in-aspnet-web-api) feature in Asp.Net framework expects the class with a default constructor to bind the incoming request to a parameter in the action method. You will get a run-time error if the class doesn't contain default constructor.

So, if we need to have a default constructor for a fsharp record type when it get compiled, we need to decorate the record type with [CLIMutable attribute](https://msdn.microsoft.com/en-us/library/hh289724.aspx)

Presence of this attribute enable the compiler to generate the default constructor and the property setters when the code get compiled to IL

{% img center border /images/fs_cs_interop/Record_Type_Attr_Compilation.png %}