---
layout: post
title: "Leveraging Property Based Testing"
date: 2016-09-30 09:06:15 +0530
comments: true
categories:
  - fsharp
  - property-based-testing
  - functional-programming
  - FsCheck
---

[Property-based testing](http://fsharpforfunandprofit.com/posts/property-based-testing/) is one of the powerful technique to unit test the code. Unlike the example-based testing (where we *Arrange* a set of example inputs to *Assert* the system under test), Property based testing enable us to focus on what we wanted to test and liberate us from doing mundane work on coming up with sample inputs.

Recently, I got a chance to use Property based testing in [an open source library](https://github.com/tamizhvendan/Suave.Azure.Functions) that I am developing to use [Suave](https://suave.io) in [Azure Functions](https://azure.microsoft.com/en-in/services/functions/). I felt very productive and it was a such a joy to write the unit tests.  

In this blog post, you are going to learn how Property-based testing has helped me and why you need to consider using(or learning) it

## Use Case

Let me start with the use case that I wanted to test. The library that I am working is an adapter between two HTTP protocol abstractions, [System.Net.Http](https://msdn.microsoft.com/en-us/library/system.net.http.aspx) and [Suave.Http](https://github.com/SuaveIO/suave/blob/v1.1.3/src/Suave/Http.fs)

The first task is to map [HttpStatusCode](https://msdn.microsoft.com/en-us/library/system.net.httpstatuscode.aspx) of *System.Net* to the [HttpCode](https://github.com/SuaveIO/suave/blob/v1.1.3/src/Suave/Http.fs#L64-L71) of *Suave.Http* and the next task is doing the vice-versa.

> *HttpStatusCode* is an enum type and *HttpCode* is a discriminated union type. Refer this [blog post](https://fsharpforfunandprofit.com/posts/enum-types/) to know more about this difference.

## Identifying the Property

The first step is Property-based testing is identifying the property. In other words, it forces to think about the relationship between the *input* and the *output*.

I believe this *thinking* will make a huge difference in the quality of the software that we deliver.

In the first task that we are about to implement, the relationship is the *integer* representation of the HTTP status code is both *HttpStatusCode* and *HttpCode*.

Programmatically, this can be asserted like

```fsharp
// boolean condition
// In F# (=) operator represents the equality checking
LanguagePrimitives.EnumToValue httpStatusCode = httpCode.code
```

> The [EnumToValue](https://msdn.microsoft.com/en-us/visualfsharpdocs/conceptual/languageprimitives.enumtovalue%5B'enum,'t%5D-function-%5Bfsharp%5D) returns the integer value associated with the enum, which is nothing but the integer representation of the HTTP status code it represents. The `code` is [a member of](https://github.com/SuaveIO/suave/blob/v1.1.3/src/Suave/Http.fs#L73-L85) *HttpCode* that represents the same integer.

If this property holds true for given set of inputs, then we can assert that the function that does the transformation is working correctly.

## Implementing the mapping function `httpStatusCode`

The initial implementation that I had in my mind was

```fsharp
let NetStatusCode = function
| HttpCode.HTTP_200 -> HttpStatusCode.OK
| HttpCode.HTTP_201 -> HttpStatusCode.Created
| HttpCode.HTTP_400 -> HttpStatusCode.BadRequest
| HttpCode.HTTP_404 -> HttpStatusCode.NotFound
| HttpCode.HTTP_202 -> HttpStatusCode.Accepted
| // ...
```
If I haven't choosen to use Property based testing, I might have ended up with this line by line mapping for all the HTTP status codes. As a matter of fact, in [my last blog post](({% post_url 2016-09-19-scale-up-azure-functions-in-f-number-using-suave%})) on using Suave in Azure Functions, I've used this same approach.

While thinking regarding properties to assert the transformation, for the first time I came to know about the [Language Primitives](https://msdn.microsoft.com/en-us/visualfsharpdocs/conceptual/core.languageprimitives-module-%5Bfsharp%5D) module in F# and the *EnumToValue* function.

There should be an another function *EnumOfValue* right?

Yes!

Let's use this in the `httpStatusCode` function implementation

```fsharp
let httpStatusCode (httpCode : HttpCode) : HttpStatusCode =
  LanguagePrimitives.EnumOfValue httpCode.code
```
Short and Sweet!

Property-based testing made my day and helped me [to save](http://keysleft.com/) a lot of keystrokes!

## Writing Our First Property based Unit Test

In F# we can use the [FsCheck](https://fscheck.github.io/FsCheck/) library to write the Property based unit tests. For this implementation, we will be using *FsCheck.Xunit* to run Property tests in [XUnit](http://xunit.github.io/)

```fsharp
open Suave.Http
open System.Net
open FsCheck
open FsCheck.Xunit

[<Property>]
let ``httpStatusCode maps Suave's HttpCode to System.Net's HttpStatusCode ``
  (httpCode : HttpCode) =

    let httpStatusCode = Response.httpStatusCode httpCode
    LanguagePrimitives.EnumToValue httpStatusCode = httpCode.code
```

The magic here is the `Property` attribute. It makes our life easier by auto-populating the parameter `httpCode` with the all the possible values and runs the unit test against each value.

As a convention, we need to return boolean by validating the property that we wanted to assert.

> There is no Arrange step to setup the input parameter and FsCheck does that for you with 100% test coverage :-)

## Mapping HttpCode to HttpStatusCode

The next task in the use case is doing the reverse, mapping *HttpStatusCode* to *HttpCode*.

Let's start with the test first.

The integer representation of HTTP status code property that we used on the previous task holds true for this mapping also.

```fsharp
[<Property>]
let ``suaveHttpCode maps System.Net's HttpStatusCode to Suave's HttpCode if exists``
  (httpStatusCode : HttpStatusCode) =
    let httpCode = suaveHttpCode httpStatusCode
    let code = LanguagePrimitives.EnumToValue httpStatusCode

    Option.isSome httpCode && httpCode.Value.code = code
```

The `suaveHttpCode` function returns an [Option type](http://fsharpforfunandprofit.com/posts/the-option-type/) as Suave.Http.HttpCode's `tryParse` function [parses the integer HTTP status code](https://github.com/SuaveIO/suave/blob/v1.1.3/src/Suave/Http.fs#L184-L194) and returns the corresponding `HttpCode` if the parsing succeeds.

The implementation of `suaveHttpCode` function is

```fsharp
let suaveHttpCode (httpStatusCode : HttpStatusCode) =
  let code =
    LanguagePrimitives.EnumToValue httpStatusCode
    |> HttpCode.tryParse
  match code with
  | Choice1Of2 httpCode -> Some httpCode
  | _ -> None
```

Now if we run the unit tests, You will be getting the following error (if you are using Suave NuGet package version <= 1.1.3)

```
Suave.Azure.Functions.Tests.suaveHttpCode maps System.Net's HttpStatusCode to Suave's HttpCode [FAIL]
FsCheck.Xunit.PropertyFailedException :
     Falsifiable, after 7 tests (0 shrinks) (StdGen (1629668181,296211181)):
     Original:
     Unused
```

The reason for this failure is Suave (up to v1.1.3) doesn't have the HTTP status code *306 (UnUsed)*. The unit test that we wrote was exercised by the *FsCheck* for the all possible values of the `HttpStatusCode` enum till it encountered this failure case.

Let's patch our unit test to fix this failure.

```fsharp
let httpCode = Response.suaveHttpCode httpStatusCode
  let code = LanguagePrimitives.EnumToValue httpStatusCode
  let unSupportedSuaveHttpCodes = [306]

  if List.contains code unSupportedSuaveHttpCodes then
    Option.isNone httpCode
  else
    Option.isSome httpCode && httpCode.Value.code = code
```

If you run the unit test after this, you will get an another failure

```
Suave.Azure.Functions.Tests.suaveHttpCode maps System.Net's HttpStatusCode to Suave's HttpCode [FAIL]
FsCheck.Xunit.PropertyFailedException :
  Falsifiable, after 10 tests (0 shrinks) (StdGen (507658935,296211184)):
  Original:
  UpgradeRequired
```

Again, The reason is HTTP Status Code `426 (UpgradeRequired)` is not supported is Suave at this point of writing. Let's patch this too by adding this to the list `unSupportedSuaveHttpCodes`.

```fsharp
// ...
let unSupportedSuaveHttpCodes = [306;426]
// ...
```

After this FsCheck is happy and we too.

> If I haven't used Property based testing, for sure, I might have missed these two unsupported HTTP status codes.

## The Spirit of Open Source

While looking at `Suave.Http` codebase to fix the unit test failures, this is what I saw in the code

{% img center border /images/leveraging_pbt/send_pr.png %}

This is the beauty of open source, and I am glad [to do it](https://github.com/SuaveIO/suave/pull/512)

## Summary

<blockquote class="twitter-tweet" data-lang="en"><p lang="en" dir="ltr">Writing tests first forces you to think about the problem you&#39;re solving. Writing property-based tests forces you to think way harder.</p>&mdash; Jessica Kerr (@jessitron) <a href="https://twitter.com/jessitron/status/327480330900611072">April 25, 2013</a></blockquote>



