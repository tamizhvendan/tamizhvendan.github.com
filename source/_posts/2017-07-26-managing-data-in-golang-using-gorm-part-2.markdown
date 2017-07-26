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
