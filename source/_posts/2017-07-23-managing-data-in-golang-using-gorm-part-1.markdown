---
layout: post
title: Managing Data in golang using gorm - Part 1
date: 2017-07-23 07:55:59 +0530
comments: true
categories: 
  - golang
  - gorm
---

[We](www.ajira.tech) have been using [gorm](jinzhu.me/gorm/) as a primary tool to interact with [PostgreSQL](https://www.PostgreSQL.org) from [golang](https://golang.org) for almost a year now. 

Gorm does an excellent job as an [ORM](https://en.wikipedia.org/wiki/Object-relational_mapping) library, and we enjoyed using it in our projects. 

Through this blog post series, I will be sharing our experiences on how we leveraged gorm to solve our client's needs.

## The Domain

In this blog post series, we will be implementing the data persistence side of a blogging platform similar to [medium](https://medium.com), called **gomidway**.  

## Part-1 Introduction

In this blog post, we will be working on defining the backend for signing up a new user and providing a provision for user login.

## The User Model

Let's start our modeling from the User table. 

```sql
CREATE TABLE users(
  id SERIAL PRIMARY KEY,
  username VARCHAR(50) UNIQUE NOT NULL,
  email VARCHAR(255) UNIQUE NOT NULL,
  password_hash TEXT NOT NULL
);
```

Then define an equivalent model in golang.

```go
type User struct {
  ID           uint `gorm:"primary_key"`
  Username     string
  Email        string
  PasswordHash string
}
```

Before taking the next steps, let me spend some time on explaining where to put this `User` *struct*

### The Folder Structure

There are two common ways to organize the models in golang. 

One approach is defining a folder called `models` and put all the models here

{% img center /images/gomidway/part1/models.png %}

The another approach is an invert of this structure. In this design, we will have a separate folder for each model. 

{% img center /images/gomidway/part1/domain.png %}

Both the approaches have pros and cons. Choosing one over the other is entirely opinionated, and my preference is the second one.

IMHO, the folder structure has [to represent the domain](https://8thlight.com/blog/uncle-bob/2011/09/30/Screaming-Architecture.html) and the code associated a domain model should coexist with proper separation of concern. 

{% img center /images/gomidway/part1/folder_structure.png%}

> Software architectures are structures that support the use cases of the system - [Ivar Jacobson](https://www.amazon.com/Object-Oriented-Software-Engineering-Driven-Approach/dp/0201403471)

In this gorm blog post series, I will be following the `domain` based folder structure. 

## Use Case #1 - User Signup

The Signup use case of a user is defined as 

- A user should sign up himself by providing his email, username, and password
- If the username or the email already exists, we need to let him now 
- We also need to let him know if there is any error while persisting his signup details
- If signup succeeds, he should be getting a unique identifier in the system

To keep things simple and the focus of this series is on the data persistence side, we are not going to discuss/implement the HTTP portion of the application. Instead, we will be driving our implementation with some hard code values during the application bootstrap. 

### Defining the Signup handler

Like many terms in software engineering, the term **handler** has different meanings. So, let me start by explaining what I mean by a handler here. 

A handler is a function that represents an use-case. It takes its dependency(ies) and its input(s) as parameters and returns the outcome of the use case. 

```go
// user/signup/handler.go
package signup

type Request struct {
  Username string
  Email    string
  Password string
}

type Response struct {
  Id uint
}

func Signup(db *gorm.DB, req *Request) (*Response, error) { 
  // ...
}
```

One important thing to notice here is, the `Request` and the `Response` are golang structs. How the request is being populated from user's request (JSON Post / HTML form Post / command from a message queue) and how the response communicated to the user (JSON response / HTML view / event in the message queue) are left up to the application boundary. 

In the Signup function, as a first step, we need to create the hash for the password using [bcrypt](https://godoc.org/golang.org/x/crypto/bcrypt) and then create the new user. 

```go
func Signup(db *gorm.DB, req *Request) (*Response, error) {
  passwordHash, err := bcrypt.GenerateFromPassword([]byte(req.Password), bcrypt.DefaultCost)
  if err != nil {
    return nil, err
  }
  newUser := &user.User{
    Username:     req.Username,
    Email:        req.Email,
    PasswordHash: string(passwordHash),
  }
  // ????
}
```

The next step is persisting this newly created user

### Adding User Create function

The create user function is straight forward. We just need to call the [Create](https://godoc.org/github.com/jinzhu/gorm#DB.Create) method in gorm

```go
// user/create.go
package user

func Create(db *gorm.DB, user *User) (uint, error) {
  err := db.Create(user).Error
  if err != nil {
    return 0, err
  }
  return user.ID, nil
}
```

But the work is not done yet!

As per our use case, We need to let the handler to know if the username or the email already exists. 

We already have unique constraints in place in the `users` table. 

```bash
gomidway=# \d users
                                    Table "public.users"
    Column     |          Type          |                     Modifiers
---------------+------------------------+----------------------------------------------------
 id            | integer                | not null default nextval('users_id_seq'::regclass)
 username      | character varying(50)  | not null
 email         | character varying(255) | not null
 password_hash | text                   | not null
Indexes:
    "users_pkey" PRIMARY KEY, btree (id)
    "users_email_key" UNIQUE CONSTRAINT, btree (email)
    "users_username_key" UNIQUE CONSTRAINT, btree (username)
``` 

So, the `Create` method in gorm would return an error of type [pq Error](http://godoc.org/github.com/lib/pq#Error) with the [ErrorCode](http://godoc.org/github.com/lib/pq#ErrorCode) as `"23505"` for `unique_violation`, and the `Constraint` field will be having the unique constraint key name `users_email_key` and `users_username_key` for email and username duplicate error respectively. 

Though this error does communicate what we wanted, it is very generic and what we want is something concrete to our use case. 

To make it happen, let's create a new folder `postgres` (aka package) and write a utility function `IsUniqueConstraintError` which checks whether the given error is a unique constraint error or not.

```go
// postgres/pq.go
func IsUniqueConstraintError(err error, constraintName string) bool {
  if pqErr, ok := err.(*pq.Error); ok {
    return pqErr.Code == "23505" && pqErr.Constraint == constraintName
  }
  return false
}
```

and then in the `model.go`, where we have the User model, add the constraint names as constants.

```go
// user/model.go
// ...
const (
  UniqueConstraintUsername = "users_username_key"
  UniqueConstraintEmail    = "users_email_key"
)
// ...
``` 

Finally, define the custom error types

```go
// user/model.go
// ...
type UsernameDuplicateError struct {
  Username string
}

func (e *UsernameDuplicateError) Error() string {
  return fmt.Sprintf("Username '%s' already exists", e.Username)
}

type EmailDuplicateError struct {
  Email string
}

func (e *EmailDuplicateError) Error() string {
  return fmt.Sprintf("Email '%s' already exists", e.Email)
}
``` 

With this new helper function, constants and types in places, we can complete the create user function as follows.

```go
// user/create.go
func Create(db *gorm.DB, user *User) (uint, error) {
  err := db.Create(user).Error
  if err != nil {
    if postgres.IsUniqueConstraintError(err, UniqueConstraintUsername) {
      return 0, &UsernameDuplicateError{Username: user.Username}
    }
    if postgres.IsUniqueConstraintError(err, UniqueConstraintEmail) {
      return 0, &EmailDuplicateError{Email: user.Email}
    }
    return 0, err
  }
  return user.ID, nil
}
```

On the handler side, we just pass the outcome of this create function to the outside(application boundary) layer

```go
// user/signup/handler.go
// ...
func Signup(db *gorm.DB, req *Request) (*Response, error) {
  // ...
  id, err := user.Create(db, newUser)
  if err != nil {
    return nil, err
  }
  return &Response{Id: id}, err
}
```

### Test Driving User Signup

As we discussed earlier, let's test drive the implementation from the application bootstrap. 

```go
// main.go
func panicOnError(err error) {
  if err != nil {
    panic(err)
  }
}

func main() {
  db, err := gorm.Open("postgres",
    `host=localhost 
      user=postgres password=test
      dbname=gomidway 
      sslmode=disable`)
  panicOnError(err)
  defer db.Close()

  signupUser(db)
}
```

```go
// main.go
// ...
func signupUser(db *gorm.DB) {
  res, err := signup.Signup(db, &signup.Request{
    Email:    "foo@bar.com",
    Username: "foo",
    Password: "foobar",
  })
  if err != nil {
    switch err.(type) {
    case *user.UsernameDuplicateError:
      fmt.Println("Bad Request: ", err.Error())
      return
    case *user.EmailDuplicateError:
      fmt.Println("Bad Request: ", err.Error())
      return
    default:
      fmt.Println("Internal Server Error: ", err.Error())
      return
    }
  }
  fmt.Println("Created: ", res.Id)
}
```

if we run the program for the first time after setting up the PostgreSQL database `gomidway` with the `users` table, we will get the following output

```bash
Created:  1
```

if we rerun the program again, we'll see the bad request error

```bash
Bad Request:  Username 'foo' already exists
```

if we modify the username from `foo` to `bar` and run the program, we'll again get a bad request error for the email address

```bash
Bad Request:  Email 'foo@bar.com' already exists
```

That's it! We have completed the first use case of **gomidway**!!

## Use Case #2 - User Login

The next use case that we are going to implement is user login. The requirement for login has been defined as

- if the user logs in with a different email address or with a different password, we need to show him appropriate errors

- if the email and the password matches, let the user know that he has logged in

### Defining the Login handler

As we did for the signup, let's start our implementation from defining the handler for login

```go
// user/login/handler.go
type Request struct {
  Email    string
  Password string
}

type Response struct {
  User *user.User
}

func Login(db *gorm.DB, req *Request) (*Response, error) { 
  // ...
}
```

To implement login, we need some help from the persistence. In other words, we have to find whether the user with the given email address exists in our application.

### Adding User FindByEmail function

Let's create a new file `find.go` in the `user` folder and define the `FindByEmail` function

```go
// user/find.go
func FindByEmail(db *gorm.DB, email string) (*User, error) {
  var user User
  res := db.Find(&user, &User{Email: email})
  return &user, res.Error
}
```

That's great. But how are we going to find if the email didn't exist in the first place?

Thankfully, We don't need to anything extra other than calling the [RecordNotFound](https://godoc.org/github.com/jinzhu/gorm#DB.RecordNotFound) to figure this out!

Let's define a custom error type `EmailNotExistsError` and return it if no records found.

```go
// user/find.go
type EmailNotExistsError struct{}

func (*EmailNotExistsError) Error() string {
  return "email not exists"
}

func FindByEmail(db *gorm.DB, email string) (*User, error) {
  var user User
  res := db.Find(&user, &User{Email: email})
  if res.RecordNotFound() {
    return nil, &EmailNotExistsError{}
  }
  return &user, nil
}
``` 

Now its time to turn our attention to Login handler to wire up the login functionality. 

```go
// user/login/handler.go
// ...
func Login(db *gorm.DB, req *Request) (*Response, error) {
  user, err := user.FindByEmail(db, req.Email)
  if err != nil {
    return nil, err
  }
  // ...
}
```

The next scenario that we need to handle is, compare the password in the request with the password hash. As we need to let the user know in case of password mismatch, let's create a `PasswordMismatchError` in the login handler and return it during the mismatch.

```go
// user/login/handler.go
// ...
type PasswordMismatchError struct{}
func (e *PasswordMismatchError) Error() string {
  return "password didn't match"
}

func Login(db *gorm.DB, req *Request) (*Response, error) {
  // ...
  err = bcrypt.CompareHashAndPassword([]byte(user.PasswordHash), []byte(req.Password))
  if err != nil {
    return nil, &PasswordMismatchError{}
  }
  return &Response{User: user}, nil
}
```

With this, we are done with login handler implementation. Let's test drive it!

### Test Driving User Login

```go
// main.go
// ...
func main() {
  // ...
  loginUser(db)
}
// ...
func loginUser(db *gorm.DB) {
  res, err := login.Login(db, &login.Request{
    Email: "foo@bar.com", 
    Password: "foobar"})
  if err != nil {
    switch err.(type) {
    case *user.EmailNotExistsError:
      fmt.Println("Bad Request: ", err.Error())
      return
    case *login.PasswordMismatchError:
      fmt.Println("Bad Request: ", err.Error())
      return
    default:
      fmt.Println("Internal Server Error: ", err.Error())
      return
    }
  }
  fmt.Printf("Ok: User '%s' logged in", res.User.Username)
}
```

## Summary

In this blog post, we have seen how we can use the create and find function in gorm along with the use case driven approach. The source code can be found in [my GitHub repository](https://github.com/tamizhvendan/gomidway/tree/part-1). 