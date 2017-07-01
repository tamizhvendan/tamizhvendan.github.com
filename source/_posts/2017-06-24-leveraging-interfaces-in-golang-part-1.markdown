---
layout: post
title: "Leveraging interfaces in golang - Part 1"
date: 2017-06-24 13:38:16 +0530
comments: true
categories: 
  - golang
---

In my previous blog post [on using golang in production]({% post_url 2017-05-01-using-golang-in-production-my-experiences %}), I have mentioned that [interfaces](https://gobyexample.com/interfaces) are my favorite feature in golang. 

As a follow-up of this comment, I would like to share how we are using (my current project is also in golang!) the interfaces to keep our code clean and consistent through a series of three blog posts

This blog post series assumes that you are familiar with the basics of interfaces in golang. If would like to know what it brings to the table, I strongly recommend to check out [this well-written article](https://hackernoon.com/why-i-like-gos-interfaces-2891adf2803c) by [Yan Cui](https://twitter.com/theburningmonk).

Let me start the series with a solution that we have implemented a few days back. 

## Some Context

The product that we are building consists of a suite of web applications. To authenticate the users, these web applications talk to a centralized web application called "Identity Server". 

Upon receiving valid login credentials, the Identity Server generates [a JSON Web Token](https://jwt.io/introduction/)(JWT) with the corresponding user claims and signs it using a public/private key pair using RSA. 

The downstream web applications will then use this JWT to grant the access to their corresponding protected resources. 

Let's assume a simple JWT claims payload

```json
{
  "sub": "1234567890",
  "name": "John D",
  "admin": true
}
```

The above payload uses one of the [JWT standard claims](https://tools.ietf.org/html/rfc7519#section-4.1), `sub`, to communicate the unique identifier of the user in the product for all the web applications. The `name` and `admin` are the custom claims. 

As per the JWT spec, [the sub claim](https://self-issued.info/docs/draft-ietf-oauth-json-web-token.html#subDef) is a case-sensitive string containing a StringOrURI value which is locally unique in the context of the issuer or be globally unique, but in our system, the unique identifier of a user is an unsigned integer(uint) type. 

As we will be sharing the JWT with other third party applications, we made a call to stick to the JWT spec and converted the uint to string type while generating the token and did the reverse while authenticating the user using this token.


## Unmarshalling JWT - A Naive Approach

Before witnessing the golang interface in action, let's see a naive implementation how we can unmarshal the claim and use.

The straightforward thing would be creating a struct matching the properties of the claim 

```go
type UserJwt struct {
  Sub    string 
  Name   string
  Admin  bool
}
```

and unmarshalling using the [json package](https://golang.org/pkg/encoding/json/) in golang 

```go
claims := `
{
  "sub": "1234567890",
  "name": "John D",
  "admin": true
}`

var userJwt *UserJwt
err := json.Unmarshal([]byte(claims), &userJwt)
```

To convert the `sub` from `string` to `uint`, we can have a method `Id` on the `UserJwt` type.

```go
func (u *UserJwt) Id() (uint, error) {
  v, err := strconv.ParseUint(u.Sub, 10, strconv.IntSize)
  if err != nil {
    return 0, err
  }
  return uint(v), nil
}
``` 

and use it after successful unmarshalling of the JWT claims. 

```go
// ...
err := json.Unmarshal([]byte(claims), &userJwt)
if err != nil {
  return err
}
id, err := userJwt.Id()
if err != nil {
  return err
}
// Do something with the id of type uint
fmt.Println(id)
```

That's great. But did you see any code smell here?

Let me share what's wrong with this approach,

Let's say that we have the following JSON claim with `sub` has the value `user/john` instead of a string representing the unsigned integer identifier of the user.

```go
claims := `
{
  "sub": "user/john",
  "name": "John D",
  "admin": true
}`
```

Unmarshalling this claim will work, and it won't return any error

```go
// ...
err := json.Unmarshal([]byte(claims), &userJwt)
if err != nil {
  return err
}

```

We can share the unmarshalled `userJwt` with the rest of the code to carry the business logic. 

We will come to know that the claim has an invalid `sub` value only when we try to get the id of the user by calling the `Id` method

```go
// ...
id, err := userJwt.Id()
// This will return the following error
// strconv.ParseUint: parsing "name/john": invalid syntax
```

If we didn't call the `Id` method, this subtle bug slip silently into the product and someday will end up as a production issue! 

In a nutshell, this approach is not robust, and the resulting code is not clean.

## Using Interfaces - A better approach

Ideally, we want the `json.Unmarshal` function should return an error if the `sub` doesn't contain a `uint` value as string.

To make it happen, we need to inform the `json.Unmarshal` function somehow to do the type conversion while unmarshalling and return an error if the conversion fails. 

How to make it happen? 

We can do this by using the [Unmarshaler](https://golang.org/pkg/encoding/json/#Unmarshaler) interface. 

In our case, we can declare the `UnmarshalJSON` method with the `UserJwt` type and in the definition, we can do the type conversion. But that'd be an overkill as we need to do the unmarshalling of the other fields, `Name`, and `Admin`, which is already working well without any custom logic. 

In other words, the effective way would be overriding the JSON unmarshalling behavior of `Sub` field alone by having the `UnmarshalJSON` method with `uint` type. But according to golang's spec we can't do it

> You can only declare a method with a receiver whose type is defined in the same package as the method. You cannot declare a method with a receiver whose type is defined in another package (which includes the built-in types such as int).

To handle this kind of scenario, we can make use of the [named types](https://golang.org/ref/spec#Type_identity) in golang and define a new type called `Sub` with an underlying type `uint`

```go
type Sub uint
```

Then we can declare the `UnmarshalJSON` method with this `Sub` type. 

```go
func (s *Sub) UnmarshalJSON(b []byte) error {
  sub := strings.Replace(string(b), `"`, "", 2)
  v, err := strconv.ParseUint(sub, 10, strconv.IntSize)
  if err != nil {
    return err
  }
  *s = Sub(uint(v))
  return nil
}
```

> Using the `Replace` function, we are getting rid of the double quotes in the actual JSON encoded value 

With this new `Sub` type in place, We can rewrite the `UserJwt` by replacing the `Sub` field with the `Id` field of type `Sub`.  

```go
type UserJwt struct {
  Id    Sub `json:"sub"`
  Name  string
  Admin bool
}
```

> The `json` tag with the value `"sub"` is required to map the `sub` key in the JSON claim with the Id. 

Now if we try to unmarshal the invalid claim, the `json.Unmarshal` function will return an error

```go
claims := `
{
  "sub": "user/john",
  "name": "John D",
  "admin": true
}`
var userJwt *UserJwt
err := json.Unmarshal([]byte(claims), &userJwt)
// This will return the following error
// strconv.ParseUint: parsing "name/john": invalid syntax
```

For a valid claim, we can now get the `Id` directly from the `UserJwt` `Id` field.

```go
claims := `
{
  "sub": "1234567890",
  "name": "John D",
  "admin": true
}`
var userJwt *UserJwt
err := json.Unmarshal([]byte(claims), &userJwt)
// ...
fmt.Println(userJwt.Id)
```



That's it! The code is in better shape now :)

## Summary

In this blog post, we have seen how we can write cleaner code by leveraging the interfaces. The source code is available in [my GitHub repository](https://github.com/tamizhvendan/igo1).