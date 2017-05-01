---
layout: post
title: "Using Golang in Production - My Experiences"
date: 2017-05-01 07:50:32 +0530
comments: true
categories: 
  - golang
  - experiences
---

# Using Golang in Production

For the last one year, I was working with the development team of a leading media entertainment company in India and developed a cloud platform to distribute the [DCPs](https://en.wikipedia.org/wiki/Digital_Cinema_Package) across the globe in a uniform, scalable and cost-effective manner.

We built the entire platform using [golang](https://golang.org/) and [event-driven microservices architecture](https://www.nginx.com/blog/event-driven-data-management-microservices/) . It was an exciting journey, and this blog post summarizes the experiences of using golang in this project.

## The Learning Curve

Golang is [a simple language](https://www.youtube.com/watch?v=rFejpH_tAHM) to learn. Any developer who has some good experience in any programming language can pick it in a month. It took me two weeks to understand the language. 

I've also learned a lot from the code review comments from the other developers of my team who are already familiar with the language. 

The official golang site has done really good job in coming up two important learning resources for the beginners.

- [A Tour of Go](https://tour.golang.org) - Through a series of in-browser hands-on tutorials you can get a good feel of the language in few hours. 
- [Effective Go](https://golang.org/doc/effective_go.html) - It is one the best introduction as well as best practices documentation of a programming language that I have read. Right from package naming convention to how to do share the state, this documentation will provide all it required to write an idiomatic golang code. 

In addition to these resources, I recommend the [Golang FAQs](https://golang.org/doc/faq) and the [GoByExample](https://gobyexample.com/) for the beginners. 

## Go Routines, Channels and Select

In my perspective, Goroutines and channels are the sweet spots of golang. The [abstraction](https://talks.golang.org/2015/simplicity-is-complicated.slide#21) it provides is elegant. It enables you to write concurrent programs without any hassles. 

We have made substantial use of golang concurrency whenever it made sense, and the benefits were incredible. The blog post on [Go Concurrency Patterns](https://blog.golang.org/pipelines) is an excellent read to get a feel of it.

## Interfaces in golang

My favorite feature in golang is its support for [interfaces](https://gobyexample.com/interfaces) . Unlike C# or Java, you don't need to specify the name of the interface when a type implements an interface. Because of this, the package in which the type present, doesn't need to refer the package in which the interface has been declared if the type implements the interface. Yan Cui has written an excellent [blog post](https://hackernoon.com/why-i-like-gos-interfaces-2891adf2803c) about its elegance. 

> The design for Go’s interface stems from the observation that **patterns and abstractions only become apparent after we’ve seen it a few times** . So rather than locking us in with abstractions at the start of a project when we’re at the point of our greatest ignorance, we can define these abstractions as and when they become apparent to us. - _Yan Cui_ 

## The Go Proverbs

The [Go Proverbs](https://go-proverbs.github.io/) provides a set of principles and guidelines while developing software in golang. Though certain principles( _A little copying is better than a little dependency._ ) look weird, they have profound meaning behind it. Like the saying, _"You need to spend time crawling alone through shadows to truly appreciate what it is to stand in the sun.", _ to appreciate these proverbs you need to spend a decent amount time in developing software in go.

## Golang Standard Library

The golang [standard library](https://golang.org/pkg/) is futuristic. It has pretty much all that is required for solving the general problems efficiently. The surprising factor for me is treating [JSON](https://golang.org/pkg/encoding/json/) as first class citizen in the standard library. Most of the languages rely on an open source library to deal with JSON. Having JSON handling as part of the standard library shows how much thought and care has been put in. 

## Declared and Not used Compiler Error

Another good thing that golang got it right is showing compiler errors for unused variables and packages. That software which we write evolve along with its business requirements. Continuous refactoring, feature addition or deletion, changing the architecture to meet the new demands are some the stuff that we do during the evolution of the software. During these activities often we miss removing the unused code and the packages that we were using before. 

Through this compiler error, golang forces the developer to write better code. The biggest advantage is while reading the code written by someone, you can be sure that the variables and the packages are always being used. 

Also removing unused packages results in smaller binary size. 

## go Command (Command Line Interface)

The [go command](https://golang.org/cmd/go/) is an another well thought out feature in golang which comes by default with the installation. Using `go command` , you can compile & build the code, download and install external packages, run the tests and lot more without relying on any other external tools. The `go test` command is really fast and it made our job easier while doing Test-Driven development. 

## go install and Single executable binary

The command which I loved the most in golang is the `go install` command. It takes care of compiling the code and produces a single executable binary. Due to caching of intermediary packages, it builds the single executable binary incredibly fast. 

To run this resulted binary file, we don't have to install anything on the target machine _(Like JVM, .NET). _ It enabled us to share the microservice as an executable file within other team members to create [spikes](http://www.extremeprogramming.org/rules/spike.html) , develop and locally test other dependent microservices. 

Just like bundling the front-end assets into single javascript file and serving them via CDNs, we can even do continuous deployment of golang programming by publishing the binaries if it makes sense! 

We can even do [cross-compilation ](https://dave.cheney.net/2015/08/22/cross-compilation-with-go-1-5) of your golang code for different OSes and Architectures using `go build` command. 

## Open Source Libraries in golang 

Golang has a thriving open source community and it houses a huge collection of [open source libraries and frameworks](https://github.com/avelino/awesome-go) . Here is the list of some of the things that we have used heavily

-  [AMQP](https://github.com/streadway/amqp) - To interact with RabbitMQ
-  [GORM](https://github.com/jinzhu/gorm) - To perform database operations in PostgreSQL
-  [go-uuid](https://github.com/satori/go.uuid) - To generate and use UUIDs
-  [migrate](https://github.com/mattes/migrate) - For database migrations
-  [envconfig](https://github.com/kelseyhightower/envconfig) - For managing configuration data from environment variables
-  [negroni](https://github.com/urfave/negroni) - To write HTTP middlewares
-  [httprouter](http://github.com/julienschmidt/httprouter) - For handling HTTP routes. We have used this along with golang's [standard HTTP handlers](https://golang.org/doc/articles/wiki/) 
-  [logrus](http://github.com/sirupsen/logrus) - Simple and powerful library for logging
-  [testify](https://github.com/stretchr/testify) - For unit test assertions

For most of the common things that you'd like to do in your project, you can always find an off the shelf open source library

## Private methods, functions and struct fields

I was admired when I came to know that just by using [a naming convention](https://golang.org/ref/spec#Exported_identifiers) , I can specify public and private methods, functions and struct fields. It is a smart feature in the language. If you want to expose them as public, start with an upper case letter and start with a lower case letter if you want to them to be used only inside a package or a struct. 

## Not So Good Features in Golang

So far in this blog post, I've shared the features that I liked very much in golang. In the rest of the post, I am about to share the not so good features in golang. The heading was intentionally started with "Not So Good" as the things that I am about to share are not bad ones according to the golang language design principles and the go proverbs that we have seen earlier.

IMHO there is no perfect programming language, and there is no silver bullet. All languages shine in certain areas and not so good in some other areas. At the end of the day, a programming language is just a tool which we use to solve the business problems. We need to pick an appropriate language that solves the problem in hand. 

> Disclaimer: The intention of this blog post is to share my experiences and provide an honest feedback to the people who is evaluating golang. 

## The Golang way of handling errors

The imperative way of handling errors is one of the [common complaint raised](http://stackoverflow.com/questions/18771569/avoid-checking-if-error-is-nil-repetition) by many people.

```go
	_, err = foobar1()
	if err != nil {
	 return err
	}
	_, err = foobar2()
	if err != nil {
	 return err
	}
	_, err = foobar3()
	if err != nil {
	 return err
	}
	// and so on
```

Rob Pike addresses this concern in a detailed blog post, [Errors are values](https://blog.golang.org/errors-are-values) and quotes a pattern using `errWriter` to avoid the repetition. It is a good design and elegantly wraps the `nil` check on the `error` . The downside of this approach is we need to write an extra wrapper for every new type to get rid of the repetitiveness. The wrapper approach may get complex if the `foobar1` , `foobar2` , and `foobar3` are addressing different concerns. 

For people who is coming from typed functional programming languages (F#, Haskell, Scala) background, like me, this may look little awkward to write it in this way. There is a [proposal](https://github.com/golang/go/issues/19991) to include Result type in golang but I feel it may not be incorporated. 

## Absence of generics

This also an another common critic in golang. There is an answer of for this question in [golang FAQ](https://golang.org/doc/faq#generics) saying, it is in pipeline but my personal opinion it may not be in near future.

There are some workarounds like using [an empty interface](https://tour.golang.org/methods/14) and casting it to appropriate types using [type assertion](https://tour.golang.org/methods/15). 

The real concern is trying to do map, filter, reduce operations over collections. Again there are workarounds using [templating and reflection](http://bouk.co/blog/idiomatic-generics-in-go/) but it comes its own cost. After fidding around with different options, We went back to the classic way of iterating through `for loop`.

## Treating nil as value

While other languages are moving away from dealing with null values and replacing it with option types, golang is taking a backward step by treating `nil` (equivalent for `null` in Go) as a valid value. So we need to be cautious while using the nil value. Lack of discipline may result in runtime exceptions (panic in go vocabulary) 

This problem can be avoided to certain extent by having a function/method to return two values, a value and error, and ensuring that the value will never be `nil`, if the error is `nil` and vice versa. Unfortuantely, this approach leads to the repetition concern that we just seen. 

## Mutable variables and imperative coding

For developers who is coming from functional programming background, the mutable variables and imperative way of writing code are speed breakers in the golang journey and like the presence of `nil`, the is also a backward step in my opinion. 
