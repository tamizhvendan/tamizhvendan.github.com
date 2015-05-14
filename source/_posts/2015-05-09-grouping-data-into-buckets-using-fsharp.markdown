---
layout: post
title: "Grouping data into buckets using fsharp"
date: 2015-05-09 16:19:06 +0530
comments: true
categories: 
  - fsharp
---

Last week I come across an interesting data grouping problem, which groups the data into different buckets based on given filters as shown in the image below

{% img center /images/fs_data_grouping/bucket_function.png %}

In this blog post I am going to share how can we solve this problem very expressively using fsharp.

## Parsing filters and translate to an Abstract Syntax Tree (AST)

The first step to solve this problem is parsing the filters and translate to an AST. The strong type system in fsharp makes this step pretty easy.

```fsharp
type Filter = 
  | GreaterThan of int 
  | Between of int * int 
  | LessThan of int
```

Now we have a type (aka AST) to represent the filter, now we need to parse the incoming raw filter which is in ```string``` format to this type.

There are **n** number of ways to achieve this.

The most idiomatic way of achieving it in fsharp is through [Partial Active Patterns](http://en.wikibooks.org/wiki/F_Sharp_Programming/Active_Patterns#Partial_Active_Patterns)

```fsharp
let (|Regex|_|) pattern input =
  let m = Regex.Match(input, pattern)
  if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
  else None
```

You can easily understand this partial active pattern by thinking it as a function with the following signature

```
string -> string -> string list option
```

i.e Takes a input, matches it against a pattern. If it matched, then returns the list of matched strings wrapped with the ```Some``` type else returns the ```None``` type.

Now you may think why we need to write this straight forward function as a partial active pattern with some weird symbols ```(|_|)``` ?

We can certainly do that but we can't do [pattern matching](http://fsharpforfunandprofit.com/posts/match-expression/) against a function. 

The best way to understand this by seeing it in action

```fsharp
// string -> Filter
let createFilter filterString =
  match filterString with    
  | Regex "^>(\d+)$" [min] -> GreaterThan(int min)
  | Regex "^(\d+)-(\d+)$" [min;max] -> Between(int min,int max)
  | Regex "^<(\d+)$" [max] -> LessThan(int max)
  | _ -> failwith "Invalid Filter"
```

The above function transforms the raw string filter into its equivalent fsharp type that we have defined before. The pattern matching makes our job very easier by declaratively saying if it matches this, then do this.

One another important benefit that we are getting here, the return value (list of matched strings) is available as a last parameter in the pattern matching. It's just comes for free. 

I'd like to give you an exercise to appreciate this excellent feature in fsharp. Try to implement the above using a function instead of a partial active patterns. 

Great! We are done with the first step. Let's move to the next one.

## Grouping the data using the filter

To do the actual grouping we need to have a function that filters the data based on the given filter. Let me call it as a predicate.

```fsharp
// Filter -> int -> bool
let createPredicate = function
  | GreaterThan min -> fun n -> n > min
  | Between (min, max) -> fun n -> n >= min && n <= max
  | LessThan max -> fun n -> n < max
```

The ```createPredicate``` function takes a filter and returns the predicate function ```int -> bool```

With the functions ```createFilter``` and ```createPredicate``` in place, we can combine them and create a new function that takes a raw filter string and translate that to a predicate. 

```fsharp
// string -> int -> bool
let createRule = createFilter >> createPredicate
```
Let me create a new record type ```BucketRule``` which holds this string and the predicate

```fsharp
type BucketRule = { Label : string; Rule : int -> bool}

let createBucketRule filterString =
  { Label = filterString; Rule = createRule filterString }
``` 

Now we have all required functions and it's time to do the data grouping

```fsharp
let createBucket numbers bucketRule =
  let bucketContent = numbers |> List.filter bucketRule.Rule
  (bucketRule.Label, bucketContent)
         
let bucketify numbers filterStrings  =
  filterStrings 
  |> List.map createBucketRule
  |> List.map (createBucket numbers)
```

## Summary

Though the problem that we have seen is so trivial, some of the cool features in fsharp make it very pleasant to solve the problem. The code snippet is available in [fssnipnet](http://fssnip.net/qZ).
