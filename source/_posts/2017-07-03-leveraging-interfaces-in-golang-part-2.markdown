---
layout: post
title: "Leveraging interfaces in golang - Part 2"
date: 2017-07-03 19:44:33 +0530
comments: true
categories: 
  - golang
---

In my previous [blog post]({% post_url 2017-06-24-leveraging-interfaces-in-golang-part-1%}), we have seen how interfaces in golang can help us to come up with a cleaner design. In this blog post, we are going to see an another interesting use case of applying golang's interfaces in creating adapters!

## Some Context

In my current project, we are using [Postgres](https://www.postgresql.org/) for persisting the application data. To make our life easier, we are using [gorm](http://jinzhu.me/gorm/) to talk to Postgres from our golang code. Things were going well and we started rolling out new features without any challenges. One beautiful day, we came across an interesting requirement which gave us a run for the money.   

The requirement is to store and retrieve an array of strings from Postgres!

{% img center /images/igo2/slice-to-array-conversion.png %}

It sounds simple on paper but while implementing it we found that it is not straightforward. Let me explain what the challenge was and how we solved it through a **Task list** example

## The Database Side

Let's assume that we have database `mytasks` with a table `tasks` to keep track of the tasks. 

The `tasks` table has the following schema 

```sql
CREATE TABLE tasks (
  id SERIAL PRIMARY KEY,
  name TEXT NOT NULL,
  is_completed BOOL NOT NULL,
  tags VARCHAR(10)[]
)
```

An important thing to note over here is that each `task` has an array of `tags` of type `varchar(10)`.


## The Golang Side
 
The equivalent [model definition](http://jinzhu.me/gorm/models.html#model-definition) of the `tasks` table would look like the following in *Golang*

```go
type Task struct {
  Id          uint
  Name        string
  IsCompleted bool
  Tags        []string
}
```

## The Challenge

Everything is set to test drive the task creation. 

Let's see what happens when we try to create a new task!

```go
package main
import (
  "fmt"

  "github.com/jinzhu/gorm"
  _ "github.com/jinzhu/gorm/dialects/postgres"
)

func panicOnError(err error) {
  if err != nil {
    panic(err)
  }
}

func CreateTask(db *gorm.DB, name string, tags []string) (uint, error) {
  newTask := &Task{Name: name, Tags: tags}
  result := db.Create(newTask)
  if result.Error != nil {
    return 0, result.Error
  }
  return newTask.Id, nil
}

func main() {
  db, err := gorm.Open("postgres",
    `host=localhost 
      user=postgres password=test
      dbname=mytasks 
      sslmode=disable`)
  panicOnError(err)
  defer db.Close()

  id, err := CreateTask(db, "test 123", []string{"personal", "test"})
  panicOnError(err)
  fmt.Printf("Task %d has been created\n", id)
}
```

When we run this program, it will panic with the following error message 

```bash
panic: sql: converting Exec argument $3 type: unsupported type []string, a slice of string
```

As the error message says, the SQL driver doesn't support `[]string`. From [the documentation](https://golang.org/pkg/database/sql/driver/#Value), we can found that the *SQL drivers* only support the following values

```bash
int64
float64
bool
[]byte
string
time.Time
``` 

So, we can't persist the `task` with the `tags` using this approach. 


## Golang's interface in Action

As a first step towards the solution, let's see how the plain SQL `insert` query provides the value for arrays in *Postgres*

```sql
INSERT INTO tasks(name,is_completed,tags) VALUES('buy milk',false,'{"home","delegate"}');
```

The clue here is the plain SQL expects the value for array as a string with the following format

```bash
'{ val1 delim val2 delim ... }'
```

> double quotes around element values if they are empty strings, contain curly braces, delimiter characters, double quotes, backslashes, or white space, or match the word NULL. Double quotes and backslashes embedded in element values will be backslash-escaped. - [Postgres Documentation](https://www.postgresql.org/docs/9.6/static/arrays.html)

That's great! All we need to do is convert the `[]string` to `string` which follows the format specified above. 

An easier approach would be changing the `Tags` field of the `Task` **struct** to `string` and do this conversion somewhere in the application code before persisting the task. 

But it's not a cleaner approach as the resulting code is not semantically correct!

Golang provides a neat solution to this problem through the [Valuer](https://golang.org/pkg/database/sql/driver/#Valuer) interface

> Types implementing Valuer interface are able to convert themselves to a driver Value.

That is we need to have a type representing the `[]string` type and implement this interface to do the type conversion. 

Like we did in the [part-1]({% post_url 2017-06-24-leveraging-interfaces-in-golang-part-1 %}) of this series, let's make use of [named types](https://golang.org/ref/spec#Type_identity) by creating a new type called `StringSlice`

```go
type StringSlice []string
```

Then we need to do the type conversion in the `Value` method

```go
func (stringSlice StringSlice) Value() (driver.Value, error) {
  var quotedStrings []string
  for _, str := range stringSlice {
    quotedStrings = append(quotedStrings, strconv.Quote(str))
  }
  value := fmt.Sprintf("{ %s }", strings.Join(quotedStrings, ","))
  return value, nil
}
```

Great! 

With this new type in place, we can change the datatype of `Tags` field from `[]string` to `StringSlice` in the `Task` struct. 

If we rerun the program, it will work as expected!!

```bash
Task 1 has been created
```

## Filter by tag

Let's move to the query side of the problem. 

We would like to get a list of tasks associated with a particular tag. 

It'd be a straightforward function that uses the [find method](http://jinzhu.me/gorm/crud.html#query) in gorm.

```go
func GetTasksByTag(db *gorm.DB, tag string) ([]Task, error) {
  tasks := []Task{}
  result := db.Find(&tasks, "? = any(tags)", tag)
  if result.Error != nil {
    return nil, result.Error
  }
  return tasks, nil
}
```

Then we need to call it from our main function 

```go
// ...
func main() {
  // ...
  tasks, err := GetTasksByTag(db, "project-x")
  panicOnError(err)
  fmt.Println(tasks)
}
```

Unfortunately, if we run the program, it will panic with the following error message

```bash
panic: sql: Scan error on column index 3: unsupported Scan, storing driver.Value type []uint8 into 
type *main.StringSlice; sql: Scan error on column index 3: unsupported Scan, 
storing driver.Value type []uint8 into type *main.StringSlice
```

As the error message says, the SQL driver unable to scan (unmarshal) the data type byte slice (`[]uint8`) into our custom type `StringSlice`. 

To fix this, we need to provide a mechanism to convert `[]uint8` to `StringSlice` which in turn will be used by the SQL driver while scanning. 

Like the `Valuer` interface, *Golang* provides [Scanner](https://golang.org/pkg/database/sql/#Scanner) interface to do the data type conversion while scanning. 

The signature of the *Scanner* interface returns an error and not the converted value. 

```go
type Scanner interface {  
  Scan(src interface{}) error
}
```
So, it implies the implementor of this interface should have a pointer receiver (`*StringSlice`) which will mutate its value upon successful conversion.

```go
func (stringSlice *StringSlice) Scan(src interface{}) error { 
  // ...
}
```

In the implementation of this interface, we just need to convert the byte slice into a string slice by converting it to a `string` (Postgres representation of array value) first, and then to `StringSlice`

```
[]uint8 --> {home,delegate} --> []string{"home", "delegate"} 
```

After successful conversion, we need to assign the converted value to the receiver (`*stringSlice`)

```go
func (stringSlice *StringSlice) Scan(src interface{}) error { 
  val, ok := src.([]byte)
  if !ok {
    return fmt.Errorf("unable to scan")
  }
  value := strings.TrimPrefix(string(val), "{")
  value = strings.TrimSuffix(value, "}")

  *stringSlice = strings.Split(value, ",")

  return nil
}
```

That's it. If we run the program now, we can see the output as expected. 

```bash
[{2 schedule meeting with the team false [project-x]} 
  {3 prepare for client demo false [slides project-x]}]
```

## Summary

In this blog post, we have seen how we can make use of `Valuer` and `Scanner` interfaces in golang to marshal and unmarshal our custom data type from the database. 

The source code can be found in [my GitHub repository](https://github.com/tamizhvendan/igo2)