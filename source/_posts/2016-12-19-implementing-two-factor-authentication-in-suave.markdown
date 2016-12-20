---
layout: post
title: "Implementing Two-Factor Authentication in Suave"
date: 2016-12-19 18:34:27 +0530
comments: true
categories: 
  - fsharp
  - suave
---

Two-factor authentication is a type of [Multi-factor authentication](https://en.wikipedia.org/wiki/Multi-factor_authentication) which adds an extra layer of security to the appliactions. 

> A good example from everyday life is the withdrawing of money from a cash machine; only the correct combination of a bank card (something that the user possesses) and a PIN (personal identification number, something that the user knows) allows the transaction to be carried out. - [Wikipedia](https://en.wikipedia.org/wiki/Multi-factor_authentication)

[Google Authenticator](https://en.wikipedia.org/wiki/Google_Authenticator) is one of the popular application that implements 
two-factor authentication services. In this blog post we are going to learn how to implement Two-factor authentication in web appliactions developed using [suave](https://suave.io)

> Disclaimer: The idea presented here is a naive implemention of Two-factor authentication. The objective here is to demonstrate how to implement it in a functional programming language, F#. Things like SSL/HTTPS, preventing CSRF and other attacks are ignored for brevity. 

## Prerequiste

This blog post assumes that you are familier with the concept of Two-factor authentication and know how Google Authenticator works. 

If you are encountering this for the first time or less familier, then check out the below resources to get a picture of what is happening behind the scene.

* [Two-step verification](https://www.google.com/landing/2step) by Google 
* [How Google Authenticator works](https://garbagecollected.org/2014/09/14/how-google-authenticator-works/)
* [A Stack-Overflow Question](https://security.stackexchange.com/questions/35157/how-does-google-authenticator-work)

We are going to use [Time based One-time Password(TOTP)](https://en.wikipedia.org/wiki/Time-based_One-time_Password_Algorithm) algorithm in this blog post

## What we will be building

We are going to build a tiny web appliaction that has an inbuilt user account with the username `foo` and the password `bar`

{% img center border /images/suave_two_factor/Login.png %}

After successful login the browser will be redirected to the *Profile* page where the user sees his name with couple of buttons. One to enable *Two-factor authentication* and another one to *logout*

{% img center border /images/suave_two_factor/Profile.png %}

Upon clicking the `Enable Two Factor Authentication` button, the user will be redirected to the *Enable Two Factor Authentication* page where the user has to scan the QR Code with the Google Authenticator App (For [Android](https://play.google.com/store/apps/details?id=com.google.android.apps.authenticator2&hl=en) or [iPhone](https://itunes.apple.com/in/app/google-authenticator/id388497605?mt=8)). Then he needs to enter the verification code to enable two factor authentication for his account. 

{% img center border /images/suave_two_factor/Enable_Two_Factor.png %}

*Google Authenticator App*
{% img center border /images/suave_two_factor/Google_Authenticator.png %}

After successeful activation, the updated *Profile* page would look like 
{% img center border /images/suave_two_factor/Profile_After_Two_Factor.png %}

Now if the user logout and login again, he will be prompted to enter the verification code 
{% img center border /images/suave_two_factor/Auth_Code_Prompt.png %}

Then the user will be redirected to his *Profile* page. 

