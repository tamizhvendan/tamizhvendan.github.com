---
layout: post
title: "Step-5 Advanced Search DSL using FParsec"
date: 2015-01-18 19:52:26 +0530
comments: true
categories: 
  - fsharp
  - fparsec
  - DSL
---

> This is step 5 of my blog series on [Web Application Development in Fsharp using ASP.NET MVC]({% post_url 2014-12-10-web-application-development-in-fsharp-using-asp-dot-net-mvc %})

In this blog post we are going create Advanced Search [DSL](http://en.wikipedia.org/wiki/Domain-specific_language) for searching Phones in the PhoneCat application that we are building using a F# parser combinator library called [FParsec](http://www.quanttec.com/fparsec/). 

{% img /images/fsharp_phonecat/step_5/search_results.png %}

As you seen in the screenshot above the DSL that we are going to build is going to build will enable us to search phones using three search filters (parameters) **weight**, **screen** and **ram**. The values of these search filters can be defined in three ways as **>**(greater than), **-**(range) and **{value}**(actual value itself i.e *=*). Each of these values should be accompanied with their unit of measures **g**, **inch** and **MB** respectively. For simplicity, I've expressed these units in singulars.

The abstract syntax tree (AST) of this external DSL is

```fsharp
type SearchFilter = 
| Ram of (float -> float<MB>)
| Weight of (float -> float<g>)
| Screen of (float -> float<inch>)

type ValueFilter = 
| Value of float 
| GreaterThan of float 
| Range of float * float

let filters : (SearchFilter * ValueFilter) list = // ... 

```

FParsec does both [lexical analysis](http://en.wikipedia.org/wiki/Lexical_analysis) (converting raw character sequence into meaningful tokens) and [parsing](http://en.wikipedia.org/wiki/Parsing) (converting tokens to an AST like the one above). FParsec has been built using pure functions from the ground up and it enable us to create complex parser by combining small parsers (aka [combinators](http://programmers.stackexchange.com/questions/117522/what-are-combinators-and-how-are-they-applied-to-programming-projects-practica)). 
