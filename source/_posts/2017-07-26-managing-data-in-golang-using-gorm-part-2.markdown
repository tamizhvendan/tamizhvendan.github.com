---
layout: post
title: "Managing Data in Golang Using Gorm - Part 2"
date: 2017-07-26 07:09:17 +0530
comments: true
categories: 
  - golang
  - gorm
---

In my previous [blog post]({% post_url 2017-07-23-managing-data-in-golang-using-gorm-part-1 %}), we have seen how to use the create and find function in gorm along with use-case driven approach. In this blog post, we will be extending our blogging platform **gomidway** by implementing an another interesting use case. 

## Use Case #3 - Publishing a new blog post

Publishing a new blog post use case invovles the following

* A user can publish a new blog post by providing a title, body of the post and a set of tags.
* If the title already exists we need to let the user to know about it.
* We also need to let him know, if publish went well. 

Though the requirement looks simple on paper, there are some complexities in terms of organizing the code and orchastrating the entire operation. 

Let's dig up and see how we can solve it!

## The Database Schema

As a first step, let's add some new tables to our existing schema which has only the `users` table.

The first one is the `posts` table

```sql
CREATE TABLE posts(
  id SERIAL PRIMARY KEY,
  title VARCHAR(50) UNIQUE NOT NULL,
  body TEXT NOT NULL,
  published_at TIMESTAMP NOT NULL,
  author_id INTEGER REFERENCES users(id)
);
```

Then we need to have an another table for `tags`

```sql
CREATE TABLE tags(
  id SERIAL PRIMARY KEY,
  name VARCHAR(50) NOT NULL
);
```

And finally a bridge table to associate `posts` and `tags`

```sql
CREATE TABLE posts_tags(
  tag_id INTEGER REFERENCES tags(id),
  post_id INTEGER REFERENCES posts(id)
);
```

*Note: a blog post can have multiple tags and a tag can have multiple blog posts associated with it*

## Model Definitions - Post and Tag 

The next step is defining equivalent models for `Post` and `Tag` in golang. 

Let's create a new folder called `tag` and define the `Tag` model.

```go
// tag/model.go
package tag

type Tag struct {
  ID   uint
  Name string
}
```

Then define the `Post` model in an another new folder `post`

```go
// post/model.go
package post

import (
  "time"

  "github.com/tamizhvendan/gomidway/tag"
)

const (
  UniqueConstraintTitle = "posts_title_key"
)

type Post struct {
  ID          uint
  Title       string
  Body        string
  AuthorID    uint
  Tags        []tag.Tag `gorm:"many2many:posts_tags;"`
  PublishedAt time.Time
}

type TitleDuplicateError struct{}

func (e *TitleDuplicateError) Error() string {
  return "title already exists"
}
```

As we have seen in [the previous blog post](https://github.com/tamizhvendan/gomidway/blob/part-1/user/create.go#L11-#L16), the constant `UniqueConstraintTitle` and the `TitleDuplicateError` are required for doing unique constraint violation error check on the `title` column and to communicate it to the application respectively. 

The important thing to notice in this model definition is the `Tags` field

```go
type Post struct {
  // ...
  Tags        []tag.Tag `gorm:"many2many:posts_tags;"`
  // ..
}
```

The `Tags` field has a [golang struct tag](https://golang.org/ref/spec#Tag) `gorm` defining the association type `many2many` and the name of the bridge table `posts_tags`.

## Implementing new blog post use case

Now we have all the models required to enable publishing a new blog post and it's time to have to go at its implementation.

Let's start by creating a new folder `publish` under `post` and add the scaffolding for the handler 

*As we have seen earlier, the folder structure represent the use case and the handler orchastrate the use case*

```go
// post/publish/handler.go
package publish

import (
  "github.com/jinzhu/gorm"
)

type Request struct {
  Title    string
  Body     string
  AuthorId uint
  Tags     []string
}

type Response struct {
  PostId uint
}

func NewPost(db *gorm.DB, req *Request) (*Response, error) { 
  // TODO
}
```

The implementation of publishing a new post involves the following steps

1. For all the `Tags` that are part of the request, we need to create an entry in the `tags` table. If it is already there, we don't need to create.

2. The next step is creating a new entry in the `posts` table with the given details. 

3. Finally, we need to associate the `Tags` with the newly created `Post` via the bridge table. 

Since it involves multiple inserts on the database side all the three steps should happen inside a transaction.

*First two steps can be executed in any order as they are independent of each other*

### Code Organization (aka Responsibility Seperation)

We discussed a little bit about code organization in the [last blog post]({% post_url 2017-07-23-managing-data-in-golang-using-gorm-part-1 %}). One important thing which can help us in the long run is having proper separation of concern in the code base. 

There are multiple ways we can separate the concern. In our case, we are organizing by use cases with `handler` driving the implementation. The handler may access the `data` layer if the use case requires. 

To keep things simple, we are not discussing about dependency injection in `handler` and `data` layer interaction here. I am planning to cover this in my future blog posts.

Back to our business, the data access logic of the three steps will be in their respective packages and the `publish` handler coordinate the entire use case logic. 

{% img center /images/gomidway/part2/code_org.png %}

#### Step 1 : Create a Tag if not exists

The first step is creating a tag if it is not there in the database. For the both new tags and the existing tags we need to get its `id` from the database to associate it with the `posts`.

Gorm has a method called [FirstOrCreate](https://godoc.org/github.com/jinzhu/gorm#DB.FirstOrCreate) to help us to implement this step. 

```go
// tag/create.go
package tag

import "github.com/jinzhu/gorm"

func CreateIfNotExists(db *gorm.DB, tagName string) (*Tag, error) {
  var tag Tag
  res := db.FirstOrCreate(&tag, Tag{Name: tagName})
  if res.Error != nil {
    return nil, res.Error
  }
  return &tag, nil
}
```

The `Id` field of the tag that we are returning will be populated by the *FirstOrCreate* method.


#### Step 2 : Creating a new Post 

It is similar to creating a new user that we saw in the last blog post

```go
// post/create.go
package post

import (
  "github.com/jinzhu/gorm"
  "github.com/tamizhvendan/gomidway/postgres"
)

func Create(db *gorm.DB, post *Post) (uint, error) {
  res := db.Create(post)
  if res.Error != nil {
    if postgres.IsUniqueConstraintError(res.Error, UniqueConstraintTitle) {
      return 0, &TitleDuplicateError{}
    }
    return 0, res.Error
  }
  return post.ID, nil
}
```

#### Step 3 : Associating Tag with Post

The final step is associating the tag with the post in the database. Gorm has a decent support for [Associations](http://jinzhu.me/gorm/associations.html). The one that we needed from gorm to carry out the current step is its [Append](https://godoc.org/github.com/jinzhu/gorm#Association.Append) method.

Let's define a `constant` in `Post` model which holds the AssociationTag

```go
// post/model.go
// ...
const (
  // ...
  AssociationTags = "Tags"
)
// ...
```

Then add a new file *tag.go* in the *post* folder and implement the third step as below

```go
// post/tag.go
package post

import (
	"github.com/jinzhu/gorm"
	"github.com/tamizhvendan/gomidway/tag"
)

func AddTag(db *gorm.DB, post *Post, tag *tag.Tag) error {
  res := db.Model(post).Association(AssociationTags).Append(tag)
  return res.Error
}
```

### Publishing New Blog Post

Now we have all the individual database layer functions ready for all the three steps and it's time to focus on the implementation of publishing a new blog post.

We already have the scaffholding in place

```go
// publish/handler.go
// ...
func NewPost(db *gorm.DB, req *Request) (*Response, error) { 
  // TODO
}
```

As a first step, let's begin a new transaction using gorm's [Begin](https://godoc.org/github.com/jinzhu/gorm#DB.Begin) method. 

```go
func NewPost(db *gorm.DB, req *Request) (*Response, error) { 
  tx := db.Begin()
  if tx.Error != nil {
    return nil, tx.Error
  }
  // TODO
}
```

Then call the `Create` function in `post` package to create a new blog post

```go
func NewPost(db *gorm.DB, req *Request) (*Response, error) { 
  // ...
  newPost := &post.Post{
    AuthorID:    req.AuthorId,
    Title:       req.Title,
    Body:        req.Body,
    PublishedAt: time.Now().UTC(),
  }
  _, err := post.Create(tx, newPost)
  if err != nil {
    return nil, err
  }
  // TODO
}
```

Then for all the tags in the request, call the `CreateIfNotExists` function in `tag` package to get its respective *Ids* and associate it with the newly created post using the `AddTag` function in the `post` package.

```go
func NewPost(db *gorm.DB, req *Request) (*Response, error) { 
  // ...
  for _, tagName := range req.Tags {
    t, err := tag.CreateIfNotExists(tx, tagName)
    if err != nil {
      tx.Rollback()
      return nil, err
    }
    err = post.AddTag(tx, newPost, t)
    if err != nil {
      tx.Rollback()
      return nil, err
    }
  }
  // TODO
}
```

A thing to note here is we are rolling back the transaction using the [RollBack](https://godoc.org/github.com/jinzhu/gorm#DB.Rollback) method in case of an error. 

The final step is committing the transaction and returning the newly created post id as response

```go
func NewPost(db *gorm.DB, req *Request) (*Response, error) { 
  // ...
  res := tx.Commit()
  if res.Error != nil {
    return nil, res.Error
  }
  return &Response{PostId: newPost.ID}, nil
}
```

### Test Driving Publish New Blog Post

Let's test drive our implementation from the `main` function with some hardcoded value

```go
// main.go
package main

import (
  // ...
  "github.com/tamizhvendan/gomidway/post"
	"github.com/tamizhvendan/gomidway/post/publish"
)
// ...
func main() {
  // ...
  publishPost(db)
}


func publishPost(db *gorm.DB) {
  res, err := publish.NewPost(db, &publish.Request{
    AuthorId: 1,
    Body:     "Golang rocks!",
    Title:    "My first gomidway post",
    Tags:     []string{"intro", "golang"},
  })
  if err != nil {
    if _, ok := err.(*post.TitleDuplicateError); ok {
      fmt.Println("Bad Request: ", err.Error())
      return
    }
    fmt.Println("Internal Server Error: ", err.Error())
    return
  }
  fmt.Println("Created : ", res.PostId)
}
```

if we run the program with these hard coded values, we will get the following output

```bash
Created : 1
```

if we rerun the program without changing anything, we will get the bad request error as expected

```bash
Bad Request: title already exists
```

## Summary

In this blog post we have seen how to perform create operation of an model having many to many relationship using gorm. The source code can be found in my [GitHub repository](https://github.com/tamizhvendan/gomidway/tree/part-2). 