---
layout: post
title: "Leveraging interfaces in golang - Part 2"
date: 2017-07-03 19:44:33 +0530
comments: true
categories: 
  - golang
---

In my previous [blog post]({% post_url 2017-06-24-leveraging-interfaces-in-golang-part-1%}), we have seen how interfaces in golang can help us to come up with a cleaner design. In this blog post, we are going to see an another interesting usecase of applying golang's interfaces in creating adapters!

## Some Context

In my current project, we are using [postgres]() for persisting the application data. To make our life easier, we are making use of [gorm]() to talk to postgres from our golang code. Things were going well and we started rolling out new features without any challenges. One fine day, we came across an interesting requirement which gave us a run for the money.   

The requirement is to store and retrieve an array of strings from postgres!

{% img center /images/igo2/slice-to-array-conversion.png %}

Let me explain how we solved it thorugh a **Task list** example

## The Database Side

