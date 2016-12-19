---
layout: post
title: ""Implementing Two-Factor Authentication in Suave""
date: 2016-12-19 18:34:27 +0530
comments: true
categories: 
  - fsharp
  - suave
---

Two-factor authentication is a type of [Multi-factor authentication](https://en.wikipedia.org/wiki/Multi-factor_authentication) 
which adds an extra layer of security to the appliactions. 

> A good example from everyday life is the withdrawing of money from a cash machine; 
only the correct combination of a bank card (something that the user possesses) and 
a PIN (personal identification number, something that the user knows) allows the transaction to be 
carried out. - [Wikipedia](https://en.wikipedia.org/wiki/Multi-factor_authentication)

[Google Authenticator](https://en.wikipedia.org/wiki/Google_Authenticator) is one of the popular application that implements 
two-factor authentication services. In this blog post we are going to learn how to implement Two-factor 
authentication in web appliactions developed using [suave](https://suave.io)

> Disclaimer: The idea presented here is a naive implemention of Two-factor authentication. The objective 
here is to demonstrate how to implement it in a functional programming language, F#. Things like SSL/HTTPS, 
preventing CSRF and other attacks are ignored for brevity. 

## Prerequiste

This blog post assumes that you are familier with the concept of Two-factor authentication and know how 
Google Authenticator works. 

If you are encountering this for the first time or less familier, then check out the below resources to 
get a picture of what is happening behind the scene.

* [Two-step verification](https://www.google.com/landing/2step) by Google 
* [How Google Authenticator works](https://garbagecollected.org/2014/09/14/how-google-authenticator-works/)
* [A Stack-Overflow Question](https://security.stackexchange.com/questions/35157/how-does-google-authenticator-work)

We are going to use [Time based One-time Password(TOTP)](https://en.wikipedia.org/wiki/Time-based_One-time_Password_Algorithm)
algorithm in this blog post

## What we will be building

 