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

As you seen in the screenshot above the DSL that we are going to build is going to build will enable us to search phones using three search filters (parameters) **weight**, **screen** and **ram**. The values of these search filters can be defined in three ways as **>**(greater than), **-**(range) and **{value}**(actual value itself i.e *=*). Each of these values should be accompanied with their unit of measures **g**, **inch** and **MB** respectively. For simplicity, I've expressed these units in singulars. The **:** symbol is used to separate Search Filter and Value Filter. Multiple filters can be used by seperating thing using the **;** symbol. 

The [abstract syntax tree (AST)](http://en.wikipedia.org/wiki/Abstract_syntax_tree) of this external DSL is

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

###FPasec 101

Lets quickly walk through the basics of FParsec.

In Fparsec all the parsers are of type ```Parser<'Result,'UserState>``` . The ```'Result``` type represents the result of the parser and the ```'UserState``` represents the internal state of the parser. 

FParsec offers lot of in-built parser functions which takes F# primitive types and return a parser type for it. 

#### pchar

```fsharp
let psearchValueSeperator = pchar ':'
let pmultiFiltersSeperator = pchar ';'
```

The ```pchar``` function takes a character and returns a parser (i.e of type ```Parser<char, 'UserState>```) which parses only the given character. 

FParsec uses a convention of prefix **p** to define all its parser function and we are also going to use the same to define our custom parser functions.

### pstring 

```fsharp
let pram = pstring "ram"
let pweight = pstring "weight"
let pscreen = pstring "ram"
```
Like ```pchar``` function, the ```pstring``` takes a string as its input and return a parser (```Parser<string, 'UserState>```) that parses only the given string. This parser is case-sensitive. If you want to have a parser which case-insensitive you can use ```pstringCI```

```fsharp
let pmb = pstringCI "MB"
let pg = pstringCI "g"
let pinch = pstringCI "inch"
```

### pfloat

```fsharp
let pvalue = pfloat
```

I hope by this time you would have been familiar with the FParsec functions. Yes, you are right ```pfloat``` parses a floating point and returns the parser for the same. But unlike ```pchar``` and ```pstrig``` it doesn't take any input and it can parse any floating point numbers. 

### |>>

So far we have seen parsers for the F# primitive types. What about our custom types ? FParsec has an answer for that too using the function (aka operator) ```|>>```. 

The signature of ```|>>``` function is
```
Parser<'a, 'u> -> (a' -> 'b) -> Parser<'b, 'u>
```
i.e this function takes two inputs, a parser of type ```'a``` and a function that transform the type ```'a``` to ```'b``` and return a parser of type ```'b```

If you remember the AST that we have defined in the beginning of the post, the search filter has been defined as a discriminated union of type ```SearchFilter```. Each fields ```Ram```, ```Screen```, ```Weight``` are being represented in the DSL by their string counterparts *"ram"*, *"screen"*, *"weight"* respectively. We can leverage this ```|>>``` function to take care of this data type transformation

```fsharp
let toLowerCase (str : string) = str.ToLowerInvariant() 

let toSearchFilter str = 
  match toLowerCase str with
  | "ram" -> Ram ((*) 1.<MB>)
  | "weight" -> Weight ((*) 1.<g>)
  | "screen" -> Screen ((*) 1.<inch>)
  | _ -> failwith "Invalid search filter"

let psearchFilter str = pstring str |>> toSearchFilter

let pram = psearchFilter "ram"
let pweight = psearchFilter "weight"
let pscreen = psearchFilter "screen"

```  
If you see the type of ```pram``` it is Parser<SearchFilter, 'u> i.e Parser for our custom type. I've created a utility intermediate parser called ```psearchFilter``` that helps in avoiding the code duplication across multiple search filter parser creation.

### >>. 

I love this function and it represents the beauty of functional programing. This function has the signature

```
Parser<'a, 'u> -> Parser<'c, 'u> -> Parser<'c, 'u>
```  

It takes two parsers of same or different types and returns a parser that parses the input using the given two parsers by applying them sequentially and returns a parser with the input type of second parser 

Lets see this action to understand it more

```
let pgreaterThan = pchar '>' >>. pfloat |>> GreaterThan
```
We have created parser here for greater than. It parses the string **">80.2"** (i.e ```pchar``` parses the **>** symbol and ```pfloat``` parses the number **80.2**. Since we have created it using ```>>.``` function, both ```pchar``` and ```pfloat``` are applied in sequence and it returns a parser of float type) and retrieve the floating number **80.2** from it. Then we have applied the ```|>>``` function which translate it our custom type ```GreaterThan``` that we defined in the AST.

The ```>>.``` is much like functional composition done by the [>> function (aka operator)](http://en.wikibooks.org/wiki/F_Sharp_Programming/Higher_Order_Functions#The_Composition_Function_.28.3C.3C_operator.29) but instead of acting at teh function level it acts at the parser level.


### .>>

It is similar to the ```>>.``` function but instead of returning a parser with the input type of second parser it returns a parser with the input type of first parser.

It has the signature
```
Parser<'a, 'u> -> Parser<'c, 'u> -> Parser<'a, 'u>
``` 

### .>>.

It is a combination of ```.>>``` and ```>>.```. i.e Instead of dropping the output of either of the given parsers, it returns a parser which parses the input and returns a parer with a tuple representing the inputs of both of the given parsers.

It has the signature
```
Parser<'a, 'u> -> Parser<'c, 'u> -> Parser<('a * 'c), 'u>
``` 
Lets see both ```.>>``` and ```.>>.``` in action

```fsharp
let prange = pfloat .>> (pchar '-') .>>. pfloat |>> Range
```

The ```prange``` parser parses the string "100-200" and returns the Range type with i.e F# type equivalent [ Range (100, 200)] of the raw string "100-200".

The ```.>>``` function here dropped the symbol **-** here (output of ```pchar``` parser) and the function ```.>>.``` picked the output of both and returned a tuple which we needed for the range.

Now you got why I said I love this function! These tiny functions allow us to create parsers by combining them. If you understand how these tiny functions work and trust me you can create complex parsers even a [C# compiler](http://trelford.com/blog/post/parsecsharp.aspx)

### attempt

This function takes parser ```p``` and execute it against the input. If it result in **fatal error**, it [backtracks](http://en.wikipedia.org/wiki/Backtracking) the parser state to its original state as if that the given input is not being consumed. 

### choice

This function takes multiple parsers say ```p1```, ```p2``` ... ```pn``` and execute it against the input in the following order.

Run the parser ```p1``` against the input. If the parsing succeed skip the rest of the parsers and return the parser else if it resulted an **non-fatal error**, backtrack the parser state to its original state and try the same with the next parser ```p2```. This continues all the way down to ```pn```.

What is the difference between [Non-Fatal Error](http://www.quanttec.com/fparsec/reference/primitives.html#members.Error) and a [Fatal Error](http://www.quanttec.com/fparsec/reference/primitives.html#members.FatalError) ?

Well, its closely associated with the internal implementation of FParsec. FParsec implements backtracking with only a "one token look-ahead". i.e if the parser consumed one input token from the stream, it modifies the internal state and if it fails after that it would result in Fatal Error. Not-Fatal Error occurs when parer error happened without consuming the input. Its little complex to explain it fully and if you need more information about it, you can find in [this detailed documentation](http://www.quanttec.com/fparsec/users-guide/parsing-alternatives.html).

```fsharp
let pgreaterThan = pchar '>' >>. pfloat |>> GreaterThan
let prange = pfloat .>> (pchar '-') .>>. pfloat |>> Range
let pvalue = pfloat |>> Value
let pvalueFilters = choice [pgreaterThan; (attempt prange); pvalue]
``` 
If you seen the AST of the DSL that we are going to implement, the value filters can be any one of greaterThan or range or value. Using ```choice``` we have implemented this "either or" parser and also we have leveraged the ```attempt``` function to get rid of parser fatal error.

Both ```prange``` and ```value```, shares the same first token i.e for "100-200MB" & "100MB", the sequence "100" is same. So while parsing after consuming the token '1' from the input sequence, FParsec assumes its a range filter and changes its internal state. By the time it reaches the token 'M' in "100MB", it would result in a fatal error saying *Expecting: '-'*. As we have used ```attempt``` here, it backtracks the parser state back to the token '1' of "100MB" and start parsing using 'pvalue' parser. Its hard to get it first time (I've spent close to an hour to figure it out) and I recommend you to spend some quality time in understanding the [documentation](http://www.quanttec.com/fparsec/users-guide/parsing-alternatives.html) if you want really want to crack it!


## Implementing the Parser

We have seen a brief overview of some of the basic building blocks of FParsec. It's time to pull all these things together and create the complete parser which parses the Search DSL.

Create a source file in the **Domain** project and name it as ```Search```. Then add the AST that we have discussed as below

```fsharp
module Search =  

  type SearchFilter = 
  | Ram of (float -> float<MB>)
  | Weight of (float -> float<g>)
  | Screen of (float -> float<inch>)

  type ValueFilter = 
  | Value of float 
  | GreaterThan of float 
  | Range of float * float
```

The next step is to install the [FParsec Nuget Package](https://www.nuget.org/packages/FParsec) in the **Domain** project. After installing it create a source file with the name ```SearchParser``` and add the parser as below

```fsharp
module SearchParser =
  
  let parseFilter filterStr = 
  
    let toLowerCase (str : string) = str.ToLowerInvariant() 

    let toSearchFilter str = 
      match toLowerCase str with
      | "ram" -> Ram ((*) 1.<MB>)
      | "weight" -> Weight ((*) 1.<g>)
      | "screen" -> Screen ((*) 1.<inch>)
      | _ -> failwith "Invalid search filter"
   
    let psearchFilter str = pstring str |>> toSearchFilter
    let psearchValueSeperator = pchar ':'
    let pmultiFiltersSeperator = pchar ';'
    let pgreaterThan = pchar '>' >>. pfloat |>> GreaterThan
    let prange = pfloat .>> (pchar '-') .>>. pfloat |>> Range
    let pvalue = pfloat |>> Value
    let pvalueFilters = choice [pgreaterThan; (attempt prange); pvalue]
    let createFilterParser psearchfilter puom  = 
      psearchfilter .>> psearchValueSeperator .>>. pvalueFilters .>> puom

    let pmb = pstringCI "MB"
    let pg = pstringCI "g"
    let pinch = pstringCI "inch"
    let pram = psearchFilter "ram"
    let pweight = psearchFilter "weight"
    let pscreen = psearchFilter "screen"

    let pramFilter = createFilterParser pram pmb
    let pweightFilter = createFilterParser pweight pg
    let pscreenFilter = createFilterParser pscreen pinch
    let pfilter = choice [pramFilter;pweightFilter;pscreenFilter]

    let parser : Parser<(SearchFilter * ValueFilter) list, unit> = (sepBy pfilter pmultiFiltersSeperator)

    match run parser filterStr with
    | Success(filters, _, _) -> Choice1Of2(filters)
    | Failure(err,_,_) -> Choice2Of2(err)
```







